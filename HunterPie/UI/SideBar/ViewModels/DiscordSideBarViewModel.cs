using HunterPie.Core.System;
using HunterPie.Domain.Common;
using HunterPie.UI.Architecture;
using System;
using System.Threading.Tasks;

namespace HunterPie.UI.SideBar.ViewModels;

internal class DiscordSideBarViewModel : ViewModel, ISideBarViewModel
{
    public Type? Type => null;

    public string Label => "//Strings/Client/Tabs/Tab[@Id='DISCORD_STRING']";

    public string Icon => "ICON_DISCORD";

    // Rise Overlay: Discord community entry hidden by default.
    public bool IsAvailable => false;

    public bool IsSelected { get; set; }

    public Task ExecuteAsync()
    {
        BrowserService.OpenUrl(CommonLinks.DISCORD);

        return Task.CompletedTask;
    }
}