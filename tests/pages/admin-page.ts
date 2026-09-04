import { Locator, Page } from '@playwright/test';

/**
 * Page Object Model for the Admin page
 * Handles authentication and file uploads through the admin UI
 */
export class AdminPage {
    readonly page: Page;

    // Authentication elements
    readonly loginButton: Locator;
    readonly logoutButton: Locator;
    readonly signedInAlert: Locator;
    readonly accessDeniedAlert: Locator;


    // Upload form elements (weekly lines)
    readonly weekInput: Locator;
    readonly yearInput: Locator;
    readonly fileInput: Locator;
    readonly uploadButton: Locator;
    readonly selectedFileAlert: Locator;

    // Bowl upload form elements
    readonly bowlYearInput: Locator;
    readonly bowlFileInput: Locator;
    readonly bowlUploadButton: Locator;
    readonly bowlSelectedFileAlert: Locator;

    // Status elements
    readonly successAlert: Locator;
    readonly errorAlert: Locator;
    readonly uploadSpinner: Locator;

    // Bowl status elements
    readonly bowlSuccessAlert: Locator;
    readonly bowlErrorAlert: Locator;
    readonly bowlUploadSpinner: Locator;

    constructor(page: Page) {
        this.page = page;

        // Authentication elements
        this.loginButton = page.getByRole('button', { name: /Sign in with Google/i });
        this.logoutButton = page.getByRole('button', { name: /Sign Out/i });
        this.signedInAlert = page.locator('.alert-info').filter({ hasText: 'Signed in as' });
        this.accessDeniedAlert = page.locator('.alert-warning').filter({ hasText: 'Access Denied' });


        // Upload form elements (weekly lines)
        this.weekInput = page.locator('#weekInput');
        this.yearInput = page.locator('#yearInput');
        this.fileInput = page.locator('#fileInput');
        this.uploadButton = page.getByRole('button', { name: /Upload Lines/i });
        this.selectedFileAlert = page.locator('.alert-info').filter({ hasText: 'Selected file' }).first();

        // Bowl upload form elements
        this.bowlYearInput = page.locator('#bowlYearInput');
        this.bowlFileInput = page.locator('#bowlFileInput');
        this.bowlUploadButton = page.getByRole('button', { name: /Upload Bowl Lines/i });
        this.bowlSelectedFileAlert = page.locator('.alert-info').filter({ hasText: 'Selected file' }).last();

        // Status elements (for weekly lines section)
        this.successAlert = page.locator('.alert-success').first();
        this.errorAlert = page.locator('.alert-danger').first();
        this.uploadSpinner = page.locator('.spinner-border').first();

        // Bowl status elements
        this.bowlSuccessAlert = page.locator('.alert-success').filter({ hasText: /bowl/i });
        this.bowlErrorAlert = page.locator('.alert-danger').filter({ hasText: /bowl/i });
        this.bowlUploadSpinner = page.locator('.spinner-border').last();
    }

    /**
     * Navigate to the admin page
     */
    async goto(): Promise<void> {
        await this.page.goto('/admin');
        await this.page.waitForLoadState('networkidle');
    }

    /**
     * Check if user is authenticated
     */
    async isAuthenticated(): Promise<boolean> {
        // If we see the upload form (weekInput visible), we're authenticated
        const weekInputVisible = await this.weekInput.isVisible().catch(() => false);
        return weekInputVisible;
    }

    /**
     * Check if login button is visible (not authenticated)
     */
    async isLoginRequired(): Promise<boolean> {
        return await this.loginButton.isVisible().catch(() => false);
    }

    /**
     * SWA mock authentication cannot represent application-owned GIS tokens.
     * Unit tests use a fake same-origin API; browser tests must not require a
     * real Google sign-in or handle a Google credential.
     */
    async loginWithMockAuth(_email: string): Promise<void> {
        throw new Error('SWA mock authentication is not supported by the GIS admin flow.');
    }

    /**
     * Upload a lines file through the admin UI
     * @param filePath - Absolute path to the Excel file
     * @param week - Week number
     * @param year - Year
     */
    async uploadLinesFile(filePath: string, week: number, year: number): Promise<void> {
        // Ensure we're on the admin page and authenticated
        if (await this.isLoginRequired()) {
            throw new Error('Not authenticated. Call loginWithMockAuth first.');
        }


        // Fill in week number
        await this.weekInput.fill(week.toString());

        // Fill in year
        await this.yearInput.fill(year.toString());

        // Upload file using the file input
        await this.fileInput.setInputFiles(filePath);

        // Wait for file to be selected (UI shows selected file info)
        await this.selectedFileAlert.waitFor({ state: 'visible', timeout: 5000 });

        // Click upload button
        await this.uploadButton.click();

        // Wait for upload to complete (spinner disappears and success shows)
        await this.uploadSpinner.waitFor({ state: 'hidden', timeout: 30000 });

        // Check for success or error
        const hasSuccess = await this.successAlert.isVisible().catch(() => false);
        const hasError = await this.errorAlert.isVisible().catch(() => false);

        if (hasError) {
            const errorText = await this.errorAlert.textContent();
            throw new Error(`Upload failed: ${errorText}`);
        }

        if (!hasSuccess) {
            throw new Error('Upload did not show success message');
        }

        console.log(`Successfully uploaded lines for Week ${week}, Year ${year} via admin UI`);
    }

    /**
     * Get the success message text
     */
    async getSuccessMessage(): Promise<string> {
        if (await this.successAlert.isVisible()) {
            return await this.successAlert.textContent() || '';
        }
        return '';
    }

    /**
     * Get the error message text
     */
    async getErrorMessage(): Promise<string> {
        if (await this.errorAlert.isVisible()) {
            return await this.errorAlert.textContent() || '';
        }
        return '';
    }

    /**
     * Upload a bowl lines file through the admin UI
     * @param filePath - Absolute path to the Excel file
     * @param year - Year for bowl season
     */
    async uploadBowlLinesFile(filePath: string, year: number): Promise<void> {
        // Ensure we're on the admin page and authenticated
        if (await this.isLoginRequired()) {
            throw new Error('Not authenticated. Call loginWithMockAuth first.');
        }


        // Fill in year for bowl lines
        await this.bowlYearInput.fill(year.toString());

        // Upload file using the bowl file input
        await this.bowlFileInput.setInputFiles(filePath);

        // Wait for file to be selected - look for any alert-info with the file name
        await this.page.waitForTimeout(1000); // Give time for file selection

        // Click bowl upload button
        await this.bowlUploadButton.click();

        // Wait for upload to complete - wait for either success or error message
        const successLocator = this.page.locator('.alert-success');
        const errorLocator = this.page.locator('.alert-danger');
        
        // Wait for either success or error to appear
        await Promise.race([
            successLocator.waitFor({ state: 'visible', timeout: 30000 }).catch(() => {}),
            errorLocator.waitFor({ state: 'visible', timeout: 30000 }).catch(() => {})
        ]);

        // Check for success or error
        const hasSuccess = await successLocator.isVisible().catch(() => false);
        const hasError = await errorLocator.isVisible().catch(() => false);

        if (hasError) {
            const errorText = await errorLocator.textContent();
            throw new Error(`Bowl upload failed: ${errorText}`);
        }

        if (!hasSuccess) {
            throw new Error('Bowl upload did not show success message');
        }

        console.log(`Successfully uploaded bowl lines for Year ${year} via admin UI`);
    }

    /**
     * Get the bowl success message text
     */
    async getBowlSuccessMessage(): Promise<string> {
        const successLocator = this.page.locator('.alert-success').last();
        if (await successLocator.isVisible()) {
            return await successLocator.textContent() || '';
        }
        return '';
    }

    /**
     * Get the bowl error message text
     */
    async getBowlErrorMessage(): Promise<string> {
        const errorLocator = this.page.locator('.alert-danger').last();
        if (await errorLocator.isVisible()) {
            return await errorLocator.textContent() || '';
        }
        return '';
    }

    /**
     * Logout from admin
     */
    async logout(): Promise<void> {
        if (await this.logoutButton.isVisible()) {
            await this.logoutButton.click();
            await this.page.waitForLoadState('networkidle');
        }
    }
}
