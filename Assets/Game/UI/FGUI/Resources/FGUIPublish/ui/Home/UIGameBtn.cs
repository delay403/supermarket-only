/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIGameBtn : GButton
    {
        public Controller m_c1;
        public Controller m_is;
        public const string URL = "ui://l851vxyybxlung";

        public static UIGameBtn CreateInstance()
        {
            return (UIGameBtn)UIPackage.CreateObject("Home", "GameBtn");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_is = GetControllerAt(1);
        }
    }
}