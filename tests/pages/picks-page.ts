import { Locator, Page } from '@playwright/test';

/**
 * Page Object Model for the Picks page
 * Handles navigation and user interactions for making weekly picks
 */
export class PicksPage {
  readonly page: Page;

  // Form elements
  readonly nameInput: Locator;
  readonly yearSelect: Locator;
  readonly weekSelect: Locator;
  readonly continueButton: Locator;

  // Game selection elements
  readonly backButton: Locator;
  readonly downloadButton: Locator;
  readonly generatedDownloadButton: Locator;
  readonly printButton: Locator;

  // Status elements
  readonly loadingSpinner: Locator;
  readonly errorAlert: Locator;
  readonly pickCountDisplay: Locator;

  constructor(page: Page) {
    this.page = page;

    // Form elements
    this.nameInput = page.locator('#userName');
    this.yearSelect = page.locator('#year');
    this.weekSelect = page.locator('#week');
    this.continueButton = page.getByRole('button', { name: 'Continue to Picks' });

    // Game selection elements
    this.backButton = page.getByRole('button', { name: /← Back to Week Selection/ });
    this.downloadButton = page.getByRole('button', { name: /Generate Your Picks/ });
    this.generatedDownloadButton = page.getByRole('button', { name: /Download File/ });
    this.printButton = page.getByRole('button', { name: /Print My Picks/ });

    // Status elements
    this.loadingSpinner = page.locator('.spinner-border');
    this.errorAlert = page.locator('.alert-danger');
    this.pickCountDisplay = page.locator('.alert-info');
  }

  /**
   * Navigate to the picks page
   */
  async goto(): Promise<void> {
    await this.page.goto('/picks');
    // Wait for the page to fully load
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Wait for loading to complete
   */
  async waitForLoadingComplete(): Promise<void> {
    // Wait for any loading spinners to disappear
    await this.loadingSpinner.waitFor({ state: 'hidden', timeout: 30000 });
  }

  /**
   * Enter the user's name
   */
  async enterName(name: string): Promise<void> {
    await this.nameInput.fill(name);
  }

  /**
   * Select year and week, then click continue
   */
  async selectWeek(year: number, week: number): Promise<void> {
    // Select year
    await this.yearSelect.selectOption(year.toString());

    // Wait for weeks to be loaded (wait for the week option to appear)
    await this.page.waitForFunction(
      (weekNum) => {
        const select = document.querySelector('#week') as HTMLSelectElement;
        if (!select) return false;
        return Array.from(select.options).some(opt => opt.value === weekNum.toString());
      },
      week,
      { timeout: 10000 }
    );

    // Select week
    await this.weekSelect.selectOption(week.toString());

    // Click continue
    await this.continueButton.click();

    // Wait for games to load (spinner disappears)
    await this.waitForLoadingComplete();

    // Wait for Blazor to fully initialize the game selection UI
    // This ensures the "Selected: 0 / 6" text is visible and event handlers are attached
    await this.page.waitForFunction(
      () => {
        const alertInfo = document.querySelector('.alert-info');
        if (!alertInfo) return false;
        const text = alertInfo.textContent || '';
        return /Selected:\s*0\s*\/\s*6/.test(text);
      },
      { timeout: 30000 }
    );

    // Additional wait to ensure Blazor event handlers are fully attached
    // Blazor WASM can show UI before interactivity is ready
    await this.page.waitForTimeout(500);
  }

  /**
   * Get all team selection buttons
   */
  async getTeamButtons(): Promise<Locator[]> {
    // Wait for buttons to appear
    await this.page.waitForSelector('.card .btn', { timeout: 10000 });

    // Get all buttons within game cards (both favorites and underdogs)
    const buttons = await this.page.locator('.card .d-grid .btn').all();
    return buttons;
  }

  /**
   * Select games by clicking on team buttons.
   * Each game has two buttons (favorite and underdog) - we click one per game.
   * Buttons are arranged in pairs, so we click every other button to select one team per game.
   * @param count - Number of games to select (default: 6)
   */
  async selectGames(count: number = 6): Promise<void> {
    // Get all team buttons
    const buttons = await this.getTeamButtons();

    if (buttons.length === 0) {
      throw new Error('No team buttons found on the page');
    }

    console.log(`Found ${buttons.length} team buttons, need to select ${count} games`);

    // Click on buttons until we have selected `count` games
    // Each game has 2 buttons (favorite and underdog), so we need to skip to every other pair
    // Button layout: [game1-fav, game1-dog, game2-fav, game2-dog, ...]
    // We click index 0, 2, 4, 6... (first team of each game)
    let selectedCount = 0;
    let gameIndex = 0; // Which game we're on (0, 1, 2, ...)

    while (selectedCount < count && (gameIndex * 2) < buttons.length) {
      // Click the first button of each game (favorite)
      const buttonIndex = gameIndex * 2;
      const button = buttons[buttonIndex];

      // Check if button is not disabled
      const isDisabled = await button.isDisabled();
      if (isDisabled) {
        console.log(`Game ${gameIndex} favorite button is disabled, trying underdog`);
        // Try the underdog button instead
        const underdogIndex = buttonIndex + 1;
        if (underdogIndex < buttons.length) {
          const underdogButton = buttons[underdogIndex];
          const underdogDisabled = await underdogButton.isDisabled();
          if (!underdogDisabled) {
            await this.clickButtonAndVerify(underdogButton, underdogIndex);
            selectedCount++;
          }
        }
        gameIndex++;
        continue;
      }

      // Get the current count from the UI before clicking
      const currentCount = await this.getSelectedPickCount();
      const expectedCount = currentCount + 1;

      // Click the button with retry logic for Blazor WASM hydration timing
      let clickSucceeded = false;
      for (let attempt = 0; attempt < 3 && !clickSucceeded; attempt++) {
        if (attempt > 0) {
          // Small delay before retry to allow Blazor to finish hydrating
          await this.page.waitForTimeout(500);
        }

        await button.click();

        // Wait briefly to see if the click registered
        try {
          await this.page.waitForFunction(
            (expected) => {
              const alertInfo = document.querySelector('.alert-info');
              if (!alertInfo) return false;
              const text = alertInfo.textContent || '';
              const match = text.match(/Selected:\s*(\d+)\s*\/\s*6/);
              return match && parseInt(match[1], 10) === expected;
            },
            expectedCount,
            { timeout: 3000 }
          );
          clickSucceeded = true;
        } catch {
          // Click didn't register, will retry
          console.log(`Click attempt ${attempt + 1} on game ${gameIndex} (button ${buttonIndex}) didn't register, retrying...`);
        }
      }

      if (clickSucceeded) {
        selectedCount++;
        console.log(`Selected game ${gameIndex}, total picks: ${selectedCount}`);
      } else {
        // Skip this game if button won't respond after retries
        console.log(`Game ${gameIndex} unresponsive after 3 attempts, skipping to next game`);
      }

      gameIndex++;
    }

    if (selectedCount < count) {
      throw new Error(`Could only select ${selectedCount} games, needed ${count}. Tried ${gameIndex} games out of ${Math.floor(buttons.length / 2)} available.`);
    }
  }

  /**
   * Helper to click a button and verify the selection registered
   */
  private async clickButtonAndVerify(button: Locator, buttonIndex: number): Promise<boolean> {
    const currentCount = await this.getSelectedPickCount();
    const expectedCount = currentCount + 1;

    for (let attempt = 0; attempt < 3; attempt++) {
      if (attempt > 0) {
        await this.page.waitForTimeout(500);
      }

      await button.click();

      try {
        await this.page.waitForFunction(
          (expected) => {
            const alertInfo = document.querySelector('.alert-info');
            if (!alertInfo) return false;
            const text = alertInfo.textContent || '';
            const match = text.match(/Selected:\s*(\d+)\s*\/\s*6/);
            return match && parseInt(match[1], 10) === expected;
          },
          expectedCount,
          { timeout: 3000 }
        );
        return true;
      } catch {
        console.log(`Click attempt ${attempt + 1} on button ${buttonIndex} didn't register, retrying...`);
      }
    }
    return false;
  }

  /**
   * Click the download picks button
   */
  async clickDownload(): Promise<void> {
    await this.downloadButton.click();
    await this.generatedDownloadButton.waitFor({ state: 'visible' });
    await this.generatedDownloadButton.click();
  }

  /**
   * Get the current number of selected picks
   */
  async getSelectedPickCount(): Promise<number> {
    const text = await this.pickCountDisplay.textContent();
    const match = text?.match(/Selected:\s*(\d+)\s*\/\s*6/);
    return match ? parseInt(match[1], 10) : 0;
  }

  /**
   * Check if download button is enabled
   */
  async isDownloadButtonEnabled(): Promise<boolean> {
    // First check if button is visible
    const isVisible = await this.downloadButton.isVisible().catch(() => false);
    if (!isVisible) return false;

    // Then check if it's enabled
    return !(await this.downloadButton.isDisabled());
  }

  /**
   * Check if an error is displayed
   */
  async hasError(): Promise<boolean> {
    return await this.errorAlert.isVisible().catch(() => false);
  }

  /**
   * Get error message text
   */
  async getErrorMessage(): Promise<string> {
    if (await this.hasError()) {
      return await this.errorAlert.textContent() || '';
    }
    return '';
  }
}
