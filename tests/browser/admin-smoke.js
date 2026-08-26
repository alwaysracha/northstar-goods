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

  async function login(email) {
    await page.goto(`${baseUrl}/account/login`, { waitUntil: 'networkidle0' });
    await page.type('#Email', email);
    await page.type('#Password', 'LocalDemo!2026');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.$eval('.form-card form', form => form.requestSubmit())]);
  }

  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 1 });
  await login('maya@example.local');
  await page.goto(`${baseUrl}/orders`, { waitUntil: 'networkidle0' });
  const history = await page.$eval('main', element => element.textContent);
  if (!history.includes('Your orders') || !history.includes('NST-2026-')) throw new Error('Customer order history did not render seeded order');
  const customerOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  await page.screenshot({ path: `${outputDir}/order-history-mobile.png`, fullPage: true });

  const cookies = await page.cookies();
  await page.deleteCookie(...cookies);
  await login('admin@northstar.local');
  await page.goto(`${baseUrl}/admin`, { waitUntil: 'networkidle0' });
  const dashboard = await page.$eval('main', element => element.textContent);
  if (!dashboard.includes('Admin dashboard')) throw new Error('Admin dashboard did not render');
  const adminOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  await page.screenshot({ path: `${outputDir}/admin-dashboard-mobile.png`, fullPage: true });

  const slug = `browser-qa-${Date.now()}`;
  await page.goto(`${baseUrl}/admin/categories/create`, { waitUntil: 'networkidle0' });
  await page.type('#Name', 'Browser QA Category');
  await page.type('#Slug', slug);
  await page.type('#DisplayOrder', '99');
  await page.type('#Description', 'Created by the automated browser acceptance test.');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.$eval('.admin-form', form => form.requestSubmit())]);
  const categories = await page.$eval('main', element => element.textContent);
  if (!categories.includes('Browser QA Category')) throw new Error('Admin category creation did not persist');

  const result = { customerHistory: true, customerOverflow, adminDashboard: true, adminOverflow, categoryCreated: true, errors };
  console.log(JSON.stringify(result, null, 2));
  await browser.close();
  if (customerOverflow || adminOverflow || errors.length) process.exit(1);
})();
