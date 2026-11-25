using Microsoft.Playwright;
using Playwright.Finexus.Base;

namespace Playwright.Finexus.Pages;

public class OtpPage : PageBase
{
    private const string OtpTitle_Heading = "h1:has-text('Ingresa el OTP'), h2:has-text('Ingresa el OTP'), h3:has-text('Ingresa el OTP')";
    private const string ValidateOtpButton_Button = "button.btn.btn-primary:has-text('Validar OTP')";

    public OtpPage(IPage page, string signingUrl) : base(page, signingUrl)
    {
    }

    private ILocator OtpTitle => Page.Locator(OtpTitle_Heading);
    private ILocator ValidateOtpButton => Page.Locator(ValidateOtpButton_Button);

    public override async Task NavigateAsync()
        => await OtpTitle.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

    public async Task<bool> IsDisplayedAsync() => await OtpTitle.First.IsVisibleAsync();

    public async Task EnterOtpAsync(string otp)
    {
        await Page.Locator(".otp-card").WaitForAsync();
        for (var i = 0; i < otp.Length; i++)
            await Page.GetByLabel($"Digit {i + 1}").PressSequentiallyAsync(otp[i].ToString());
        await Assertions.Expect(Page.GetByLabel("Digit 1")).ToHaveValueAsync(otp[0].ToString());
    }

    public async Task SubmitAsync() => await ValidateOtpButton.ClickAsync();
}
