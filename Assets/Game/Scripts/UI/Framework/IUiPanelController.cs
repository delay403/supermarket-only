namespace Game.UI.Framework
{
    public interface IUiPanelController
    {
        UiPanelId PanelId { get; }

        void Open(UiOpenPayload payload = null);

        void Close();
    }
}
