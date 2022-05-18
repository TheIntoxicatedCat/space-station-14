using Content.Client.Chat.UI;
using Content.Client.Info;
using Content.Client.Preferences;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Lobby.UI
{
    [GenerateTypedNameReferences]
    internal sealed partial class LobbyGui : Control
    {
        public LobbyGui(IEntityManager entityManager,
            IClientPreferencesManager preferencesManager)
        {
            RobustXamlLoader.Load(this);
            ServerName.HorizontalExpand = true;
            ServerName.HorizontalAlignment = HAlignment.Center;
        }
    }
}