using Assist.Game.Services;
using Avalonia.Controls;

namespace Assist.Game.Views.Live.Pages
{
    public partial class PregamePageView : UserControl
    {
        public PregamePageView()
        {
            LiveViewNavigationController.CurrentPage = LivePage.PREGAME;
            InitializeComponent();
        }
    }
}
