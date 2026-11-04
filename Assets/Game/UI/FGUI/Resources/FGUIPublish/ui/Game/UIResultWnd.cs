/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIResultWnd : GComponent
    {
        public Controller m_result;
        public GGraph m_bg;
        public GButton m_share;
        public const string URL = "ui://6laopsucn2cddh";

        public static UIResultWnd CreateInstance()
        {
            return (UIResultWnd)UIPackage.CreateObject("Game", "ResultWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_result = GetControllerAt(0);
            m_bg = (GGraph)GetChildAt(0);
            m_share = (GButton)GetChildAt(1);
        }
    }
}