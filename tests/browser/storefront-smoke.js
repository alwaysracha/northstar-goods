const puppeteer = require('puppeteer');
const fs = require('fs');

(async () => {
  const outputDir = process.env.ARTIFACT_DIR || '.hermes/artifacts';
  const baseUrl = (process.env.BASE_URL || 'http://localhost:5180').replace(/\/$/, '');
  fs.mkdirSync(outputDir, { recursive: true });
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errors = [];
  page.on('console', message => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('pageerror', error => errors.push(`page: ${error.message}`));
  page.on('requestfailed', request => errors.push(`request: ${request.url()} ${request.failure()?.errorText}`));
  page.on('response', response => {
    if (response.status() >= 400) errors.push(`response: ${response.status()} ${response.url()}`);
  });

  await page.setViewport({ width: 1440, height: 1000, deviceScaleFactor: 1 });
  await page.goto(`${baseUrl}`, { waitUntil: 'networkidle0' });
  const title = await page.title();
  const heading = await page.$eval('h1', element => element.textContent.trim());
  const products = await page.$$eval('.product-card', elements => elements.length);
  const desktopOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  await page.screenshot({ path: `${outputDir}/storefront-desktop.png`, fullPage: true });

  await page.goto(`${baseUrl}/catalog?search=lamp&sort=price-asc`, { waitUntil: 'networkidle0' });
  const filteredText = await page.$eval('main', element => element.textContent);
  const productLink = await page.$eval('a[href^="/products/"]', element => element.href);
  await page.goto(productLink, { waitUntil: 'networkidle0' });
  const detailHeading = await page.$eval('h1', element => element.textContent.trim());

  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 1 });
  await page.goto(`${baseUrl}`, { waitUntil: 'networkidle0' });
  const mobileOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  await page.screenshot({ path: `${outputDir}/storefront-mobile.png`, fullPage: true });

  const result = { title, heading, products, desktopOverflow, mobileOverflow, filtered: filteredText.includes('products found'), detailHeading, errors };
  console.log(JSON.stringify(result, null, 2));
  await browser.close();

  if (!title.includes('Northstar Goods') || !heading || products < 1 || desktopOverflow || mobileOverflow || !result.filtered || !detailHeading || errors.length) {
    process.exit(1);
  }
})();
