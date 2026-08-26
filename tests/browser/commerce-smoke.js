const puppeteer = require('puppeteer');
const fs = require('fs');

(async () => {
  const outputDir = process.env.ARTIFACT_DIR || '.hermes/artifacts';
  const baseUrl = (process.env.BASE_URL || 'http://localhost:5180').replace(/\/$/, '');
  fs.mkdirSync(outputDir, { recursive: true });
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(`page: ${error.message}`));
  page.on('requestfailed', request => errors.push(`request: ${request.url()} ${request.failure()?.errorText}`));
  page.on('response', response => { if (response.status() >= 400) errors.push(`response: ${response.status()} ${response.url()}`); });

  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 1 });
  await page.goto(`${baseUrl}/products/task-lamp`, { waitUntil: 'networkidle0' });
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('.add-control button')]);
  await page.goto(`${baseUrl}/cart`, { waitUntil: 'networkidle0' });
  const cartBefore = await page.$eval('main', element => element.textContent);
  if (!cartBefore.includes('Task Lamp')) throw new Error('Cart did not contain Task Lamp');

  await page.type('#code', 'welcome10');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('form[action*="ApplyDiscount"] button')]);
  const cartAfter = await page.$eval('main', element => element.textContent);
  if (!cartAfter.includes('Discount')) throw new Error('Discount was not applied');
  await page.screenshot({ path: `${outputDir}/cart-mobile.png`, fullPage: true });

  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('a[href="/Checkout"]')]);
  await page.type('#ContactEmail', 'browser.guest@example.local');
  await page.type('#RecipientName', 'Browser Guest');
  await page.type('#Line1', '42 Test Avenue');
  await page.type('#City', 'Austin');
  await page.type('#Region', 'TX');
  await page.type('#PostalCode', '78701');
  await page.$eval('#CountryCode', input => { input.value = ''; });
  await page.type('#CountryCode', 'US');
  await page.screenshot({ path: `${outputDir}/checkout-mobile.png`, fullPage: true });
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('.checkout-form button[type="submit"]')]);
  const confirmation = await page.$eval('main', element => element.textContent);
  const confirmationUrl = page.url();
  if (!confirmation.toLowerCase().includes('confirmed') && !confirmation.toLowerCase().includes('thank')) throw new Error('Confirmation page was not reached');
  await page.screenshot({ path: `${outputDir}/confirmation-mobile.png`, fullPage: true });

  await page.goto(`${baseUrl}/account/login`, { waitUntil: 'networkidle0' });
  await page.type('#Email', 'maya@example.local');
  await page.type('#Password', 'LocalDemo!2026');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.$eval('.form-card form', form => form.requestSubmit())]);
  const loginSucceeded = page.url() === `${baseUrl}/` || (await page.content()).includes('Sign out');
  if (!loginSucceeded) throw new Error('Seeded customer login failed');

  const result = { cartHasItem: true, discountApplied: true, confirmationUrl, loginSucceeded, errors };
  console.log(JSON.stringify(result, null, 2));
  await browser.close();
  if (errors.length) process.exit(1);
})();
