using Microsoft.Playwright;

public interface IPdfService
{
    Task<byte[]> HtmlToPdfAsync(string html);
}

public class PdfService : IPdfService
{
    public async Task<byte[]> HtmlToPdfAsync(string html)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true
        });
    }
}

