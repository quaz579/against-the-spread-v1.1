import { expect, test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { cleanupDownloads, getDefaultDownloadDir, waitForDownloadAndSave } from '../helpers/download-helper';
import { validateBowlPicksExcel } from '../helpers/excel-validator';
import { TestEnvironment } from '../helpers/test-environment';
import { AdminPage } from '../pages/admin-page';
import { BowlPicksPage } from '../pages/bowl-picks-page';

// Test constants
const TEST_NAME = 'Bowl E2E Test User';
const TEST_YEAR = 2025;
const DOWNLOAD_DIR = getDefaultDownloadDir();

// Get repository root path
const REPO_ROOT = path.resolve(__dirname, '../..');
const REFERENCE_DOCS = path.join(REPO_ROOT, 'reference-docs');

// Test environment configuration
const testEnv = new TestEnvironment();

// These legacy fixtures depended on SWA CLI's forged X-MS-CLIENT-PRINCIPAL.
// Application-owned GIS credentials cannot be fabricated in an E2E browser;
// keep this suite disabled until it seeds Azurite outside the protected API.
test.describe.skip('Bowl Picks Complete Flow', () => {

  test.beforeAll(async () => {
    console.log('Bowl E2E tests starting...');
    console.log(`Admin email for mock auth: ${testEnv.adminEmail}`);
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test.beforeEach(async ({ page }) => {
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test.afterAll(async () => {
    cleanupDownloads(DOWNLOAD_DIR);
  });

  test('Complete Bowl Flow: Admin uploads lines, user makes picks, downloads and validates Excel', async ({ page }) => {
    const adminPage = new AdminPage(page);
    const bowlPicksPage = new BowlPicksPage(page);
    const bowlLinesFile = path.join(REFERENCE_DOCS, 'Bowl-Lines-2.xlsx');

    // === STEP 1: Verify test data exists ===
    await test.step('Verify test data exists', async () => {
      expect(fs.existsSync(bowlLinesFile), `Bowl lines file should exist at ${bowlLinesFile}`).toBe(true);
      console.log(`Bowl lines test file found: ${bowlLinesFile}`);
    });

    // === STEP 2: Login to admin page ===
    await test.step('Login to admin page', async () => {
      await adminPage.goto();

      // Take screenshot of admin login page
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-01-admin-login.png'),
        fullPage: true 
      });

      // Login using mock authentication
      await adminPage.loginWithMockAuth(testEnv.adminEmail);

      // Verify we're authenticated
      const isAuthenticated = await adminPage.isAuthenticated();
      expect(isAuthenticated).toBe(true);

      // Take screenshot of authenticated admin page
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-02-admin-authenticated.png'),
        fullPage: true 
      });
    });

    // === STEP 3: Upload bowl lines via admin UI ===
    await test.step('Upload bowl lines via admin UI', async () => {
      // Scroll down to see bowl upload section
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      await page.waitForTimeout(500);

      // Take screenshot showing bowl upload section
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-03-admin-bowl-section.png'),
        fullPage: true 
      });

      // Upload bowl lines file
      await adminPage.uploadBowlLinesFile(bowlLinesFile, TEST_YEAR);

      // Take screenshot of successful upload
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-04-admin-upload-success.png'),
        fullPage: true 
      });

      console.log('Successfully uploaded bowl lines via admin UI');
    });

    // === STEP 4: Navigate to bowl picks page ===
    await test.step('Navigate to bowl picks page', async () => {
      await bowlPicksPage.goto();
      await bowlPicksPage.waitForLoadingComplete();

      // Take screenshot of initial bowl picks page
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-05-picks-initial.png'),
        fullPage: true 
      });
    });

    // === STEP 5: Enter user name ===
    await test.step('Enter user name', async () => {
      await bowlPicksPage.enterName(TEST_NAME);
      await expect(bowlPicksPage.nameInput).toHaveValue(TEST_NAME);

      // Take screenshot after entering name
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-06-picks-name-entered.png'),
        fullPage: true 
      });
    });

    // === STEP 6: Select year and load bowl games ===
    let gameCount = 0;
    await test.step('Select year and load bowl games', async () => {
      await bowlPicksPage.selectYearAndContinue(TEST_YEAR);

      // Check if games are loaded
      const hasError = await bowlPicksPage.hasError();
      const gamesLoaded = await bowlPicksPage.areGamesLoaded();

      if (hasError) {
        const errorMsg = await bowlPicksPage.getErrorMessage();
        console.log(`Error: ${errorMsg}`);
        await page.screenshot({ 
          path: path.join(DOWNLOAD_DIR, 'bowl-07-picks-error.png'),
          fullPage: true 
        });
        throw new Error(`Bowl lines not available: ${errorMsg}`);
      }

      expect(gamesLoaded).toBe(true);
      gameCount = await bowlPicksPage.getGameCount();
      console.log(`Loaded ${gameCount} bowl games`);

      // Take screenshot of games loaded
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-07-picks-games-loaded.png'),
        fullPage: true 
      });

      expect(gameCount).toBeGreaterThan(0);
    });

    // === STEP 7: Make picks for all games ===
    await test.step('Make picks for all games', async () => {
      // Make picks for each game
      await bowlPicksPage.makeAllPicks(gameCount);

      // Wait for UI to update
      await page.waitForTimeout(500);

      // Take screenshot of completed picks (scroll to show progress)
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.waitForTimeout(200);
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-08-picks-all-made.png'),
        fullPage: true 
      });

      console.log(`Made picks for ${gameCount} bowl games`);
    });

    // === STEP 8: Verify validation passes ===
    await test.step('Verify validation passes', async () => {
      // Check if confidence sum is valid
      const isConfidenceValid = await bowlPicksPage.isConfidenceSumValid();
      
      // Check download button is enabled
      const isDownloadEnabled = await bowlPicksPage.isDownloadButtonEnabled();
      
      console.log(`Confidence valid: ${isConfidenceValid}, Download enabled: ${isDownloadEnabled}`);
      
      // At minimum, the download button should be visible (enabled state depends on validation)
      const downloadButton = bowlPicksPage.downloadButton;
      await expect(downloadButton).toBeVisible();
    });

    // === STEP 9: Download Excel file ===
    let downloadPath: string = '';
    await test.step('Download Excel file', async () => {
      // Click download button
      downloadPath = await waitForDownloadAndSave(
        page,
        async () => await bowlPicksPage.clickDownload(),
        DOWNLOAD_DIR
      );

      // Verify file was downloaded
      expect(fs.existsSync(downloadPath), `Downloaded file should exist at ${downloadPath}`).toBe(true);
      console.log(`Bowl picks downloaded to: ${downloadPath}`);

      // Take screenshot after download
      await page.screenshot({ 
        path: path.join(DOWNLOAD_DIR, 'bowl-09-picks-downloaded.png'),
        fullPage: true 
      });
    });

    // === STEP 10: Validate Excel file structure ===
    await test.step('Validate Excel file structure', async () => {
      const validation = await validateBowlPicksExcel(downloadPath, TEST_NAME, gameCount);

      console.log('Bowl Excel validation result:', validation);

      if (!validation.isValid) {
        console.error('Bowl Excel validation errors:', validation.errors);
      }

      // Check basic validity
      expect(validation.isValid).toBe(true);
      expect(validation.errors).toHaveLength(0);
    });
  });

  test('Admin Bowl Upload: Shows upload form and handles file selection', async ({ page }) => {
    const adminPage = new AdminPage(page);

    // Navigate and login
    await adminPage.goto();
    await adminPage.loginWithMockAuth(testEnv.adminEmail);

    // Scroll to bowl section
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(500);

    // Verify bowl upload elements exist
    await expect(adminPage.bowlYearInput).toBeVisible();
    await expect(adminPage.bowlFileInput).toBeVisible();
    await expect(adminPage.bowlUploadButton).toBeVisible();

    // Take screenshot of bowl upload section
    await page.screenshot({ 
      path: path.join(DOWNLOAD_DIR, 'bowl-admin-upload-form.png'),
      fullPage: true 
    });
  });

  test('Bowl Picks: validation shows errors for duplicate confidence points', async ({ page }) => {
    const adminPage = new AdminPage(page);
    const bowlPicksPage = new BowlPicksPage(page);
    const bowlLinesFile = path.join(REFERENCE_DOCS, 'Bowl Lines Test.xlsx');

    // First upload bowl lines
    await adminPage.goto();
    await adminPage.loginWithMockAuth(testEnv.adminEmail);
    
    try {
      await adminPage.uploadBowlLinesFile(bowlLinesFile, TEST_YEAR);
    } catch {
      console.log('Bowl lines may already be uploaded, continuing...');
    }

    // Navigate to bowl picks page
    await bowlPicksPage.goto();
    await bowlPicksPage.waitForLoadingComplete();

    // Enter name and load games
    await bowlPicksPage.enterName('Validation Test User');
    await bowlPicksPage.selectYearAndContinue(TEST_YEAR);

    // Check if games are loaded
    const gamesLoaded = await bowlPicksPage.areGamesLoaded();
    
    if (!gamesLoaded) {
      console.log('Skipping validation test - no bowl lines available');
      test.skip(true, 'No bowl lines available for testing');
      return;
    }

    const gameCount = await bowlPicksPage.getGameCount();
    if (gameCount < 2) {
      test.skip(true, 'Not enough games for validation test');
      return;
    }

    // Select same confidence for two games (should show duplicate warning)
    await bowlPicksPage.selectSpreadPick(1, true);
    await bowlPicksPage.selectConfidence(1, 1);
    await bowlPicksPage.selectOutrightWinner(1, true);

    await bowlPicksPage.selectSpreadPick(2, true);
    await bowlPicksPage.selectConfidence(2, 1); // Same confidence as game 1 - DUPLICATE!
    await bowlPicksPage.selectOutrightWinner(2, true);

    // Wait for UI to update
    await page.waitForTimeout(500);

    // Take screenshot showing duplicate warning
    await page.screenshot({ 
      path: path.join(DOWNLOAD_DIR, 'bowl-validation-duplicate.png'),
      fullPage: true 
    });

    // Verify duplicate warning is shown
    const hasDuplicateWarning = await bowlPicksPage.hasDuplicateConfidenceWarning();
    expect(hasDuplicateWarning).toBe(true);

    console.log('Duplicate confidence warning is displayed correctly');
  });
});
