import { expect, test } from '@playwright/test';

test('installed iOS PWA shares a generated workbook through the native file sheet', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'standalone', {
      configurable: true,
      value: true
    });
    Object.defineProperty(navigator, 'canShare', {
      configurable: true,
      value: (data: ShareData) =>
        Array.isArray(data.files) && data.files.length === 1
    });
    Object.defineProperty(navigator, 'share', {
      configurable: true,
      value: async (data: ShareData) => {
        const file = data.files?.[0];
        (window as any).__sharedWorkbook = file
          ? { name: file.name, type: file.type, size: file.size }
          : null;
      }
    });

    (window as any).__downloadAnchorClicked = false;
    HTMLAnchorElement.prototype.click = function () {
      (window as any).__downloadAnchorClicked = true;
    };
  });

  await page.goto('/');
  await page.evaluate(() => {
    const button = document.createElement('button');
    button.textContent = 'Save generated workbook';
    button.addEventListener('click', () => {
      void (window as any).downloadFile(
        'iPhone_User_Week_1_Picks.xlsx',
        'UEsDBA==');
    });
    document.body.appendChild(button);
  });
  await page.getByRole('button', { name: 'Save generated workbook' }).click();

  await expect.poll(() => page.evaluate(() => (window as any).__sharedWorkbook)).toEqual({
    name: 'iPhone_User_Week_1_Picks.xlsx',
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    size: 4
  });
  expect(await page.evaluate(() => (window as any).__downloadAnchorClicked)).toBe(false);
});
