/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIPopup : GComponent
    {
        public Controller m_c1;
        public GTextField m_title;
        public UIGameBtn m_coinReturn;
        public UIGameBtn m_coinYes;
        public UIGameBtn m_powerReturn;
        public UIGameBtn m_powerYes;
        public const string URL = "ui://l851vxyyk8pln0";

        public static UIPopup CreateInstance()
        {
            return (UIPopup)UIPackage.CreateObject("Home", "Popup");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_title = (GTextField)GetChildAt(1);
            m_coinReturn = (UIGameBtn)GetChildAt(2);
            m_coinYes = (UIGameBtn)GetChildAt(3);
            m_powerReturn = (UIGameBtn)GetChildAt(5);
            m_powerYes = (UIGameBtn)GetChildAt(6);
        }
    }
}