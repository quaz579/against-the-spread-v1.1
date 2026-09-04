import { expect, test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { cleanupDownloads, getDefaultDownloadDir, waitForDownloadAndSave } from '../helpers/download-helper';
import { validatePicksExcel } from '../helpers/excel-validator';
import { TestEnvironment } from '../helpers/test-environment';
import { AdminPage } from '../pages/admin-page';
import { PicksPage } from '../pages/picks-page';

// Test constants
const TEST_NAME = 'E2E Test User';
const TEST_YEAR = 2025;
const TEST_WEEK_11 = 11;
const TEST_WEEK_12 = 12;
const DOWNLOAD_DIR = getDefaultDownloadDir();

// Get repository root path
const REPO_ROOT = path.resolve(__dirname, '../..');
const REFERENCE_DOCS = path.join(REPO_ROOT, 'reference-docs');

// Test environment configuration
const testEnv = new TestEnvironment();

// These legacy fixtures depended on SWA CLI's forged X-MS-CLIENT-PRINCIPAL.
// Application-owned GIS credentials cannot be fabricated in an E2E browser;
// keep this suite disabled until it seeds Azurite outside the protected API.
test.describe.skip('Complete User Flow', () => {

  test.beforeAll(async () => {
    console.log('E2E tests starting...');
    console.log(`Admin email for mock auth: ${testEnv.adminEmail}`);

    // Clean up any previous downloads
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test.beforeEach(async ({ page }) => {
    // Clean up downloads before each test
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test.afterAll(async () => {
    // Clean up downloads after all tests
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test('Week 11: upload lines via admin UI and complete user picks flow', async ({ page }) => {
    const adminPage = new AdminPage(page);
    const picksPage = new PicksPage(page);

    // === STEP 1: Login to admin and upload Week 11 lines ===
    await test.step('Login to admin and upload Week 11 lines', async () => {
      const linesFile = path.join(REFERENCE_DOCS, 'Week 11 Lines.xlsx');

      // Verify the file exists
      expect(fs.existsSync(linesFile), `Week 11 lines file should exist at ${linesFile}`).toBe(true);

      // Navigate to admin page
      await adminPage.goto();

      // Login using mock authentication
      await adminPage.loginWithMockAuth(testEnv.adminEmail);

      // Upload lines file through admin UI
      await adminPage.uploadLinesFile(linesFile, TEST_WEEK_11, TEST_YEAR);
    });

    // === STEP 2: Navigate to picks page ===
    await test.step('Navigate to picks page', async () => {
      await picksPage.goto();
      await picksPage.waitForLoadingComplete();
    });

    // === STEP 3: Enter user name ===
    await test.step('Enter user name', async () => {
      await picksPage.enterName(TEST_NAME);
      await expect(picksPage.nameInput).toHaveValue(TEST_NAME);
    });

    // === STEP 4: Select year and week ===
    await test.step('Select year and week', async () => {
      await picksPage.selectWeek(TEST_YEAR, TEST_WEEK_11);

      // Verify games are displayed
      const buttons = await picksPage.getTeamButtons();
      expect(buttons.length).toBeGreaterThan(0);
    });

    // === STEP 5: Select 6 games ===
    await test.step('Select 6 games', async () => {
      await picksPage.selectGames(6);

      // Verify 6 picks were selected
      const pickCount = await picksPage.getSelectedPickCount();
      expect(pickCount).toBe(6);

      // Verify download button is enabled
      const isEnabled = await picksPage.isDownloadButtonEnabled();
      expect(isEnabled).toBe(true);
    });

    // === STEP 6: Download Excel file ===
    let downloadPath: string;
    await test.step('Download Excel file', async () => {
      downloadPath = await waitForDownloadAndSave(
        page,
        async () => await picksPage.clickDownload(),
        DOWNLOAD_DIR
      );

      // Verify file was downloaded
      expect(fs.existsSync(downloadPath), `Downloaded file should exist at ${downloadPath}`).toBe(true);
    });

    // === STEP 7: Validate Excel file structure ===
    await test.step('Validate Excel file structure', async () => {
      const validation = await validatePicksExcel(downloadPath, TEST_NAME, 6);

      if (!validation.isValid) {
        console.error('Excel validation errors:', validation.errors);
      }

      expect(validation.isValid).toBe(true);
      expect(validation.errors).toHaveLength(0);
    });
  });

  test('Week 12: upload lines via admin UI and complete user picks flow', async ({ page }) => {
    const adminPage = new AdminPage(page);
    const picksPage = new PicksPage(page);

    // === STEP 1: Login to admin and upload Week 12 lines ===
    await test.step('Login to admin and upload Week 12 lines', async () => {
      const linesFile = path.join(REFERENCE_DOCS, 'Week 12 Lines.xlsx');

      // Verify the file exists
      expect(fs.existsSync(linesFile), `Week 12 lines file should exist at ${linesFile}`).toBe(true);

      // Navigate to admin page
      await adminPage.goto();

      // Login using mock authentication
      await adminPage.loginWithMockAuth(testEnv.adminEmail);

      // Upload lines file through admin UI
      await adminPage.uploadLinesFile(linesFile, TEST_WEEK_12, TEST_YEAR);
    });

    // === STEP 2: Navigate to picks page ===
    await test.step('Navigate to picks page', async () => {
      await picksPage.goto();
      await picksPage.waitForLoadingComplete();
    });

    // === STEP 3: Enter user name ===
    await test.step('Enter user name', async () => {
      await picksPage.enterName(TEST_NAME);
    });

    // === STEP 4: Select year and week ===
    await test.step('Select year and week', async () => {
      await picksPage.selectWeek(TEST_YEAR, TEST_WEEK_12);

      // Verify games are displayed
      const buttons = await picksPage.getTeamButtons();
      expect(buttons.length).toBeGreaterThan(0);
    });

    // === STEP 5: Select 6 games ===
    await test.step('Select 6 games', async () => {
      await picksPage.selectGames(6);

      // Verify 6 picks were selected
      const pickCount = await picksPage.getSelectedPickCount();
      expect(pickCount).toBe(6);
    });

    // === STEP 6: Download and validate Excel file ===
    await test.step('Download and validate Excel file', async () => {
      const downloadPath = await waitForDownloadAndSave(
        page,
        async () => await picksPage.clickDownload(),
        DOWNLOAD_DIR
      );

      expect(fs.existsSync(downloadPath)).toBe(true);

      const validation = await validatePicksExcel(downloadPath, TEST_NAME, 6);
      expect(validation.isValid).toBe(true);
    });
  });
});

