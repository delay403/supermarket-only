/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Setting
{
    public partial class UIcheckBtn : GButton
    {
        public Controller m_check;
        public const string URL = "ui://el0ns6e9elmh3";

        public static UIcheckBtn CreateInstance()
        {
            return (UIcheckBtn)UIPackage.CreateObject("Setting", "checkBtn");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_check = GetControllerAt(0);
        }
    }
}