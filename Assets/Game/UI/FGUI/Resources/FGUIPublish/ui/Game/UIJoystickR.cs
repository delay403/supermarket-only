/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIJoystickR : GComponent
    {
        public UIctrCompR m_ctrl;
        public const string URL = "ui://6laopsucqu8na1";

        public static UIJoystickR CreateInstance()
        {
            return (UIJoystickR)UIPackage.CreateObject("Game", "JoystickR");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_ctrl = (UIctrCompR)GetChildAt(0);
        }
    }
}