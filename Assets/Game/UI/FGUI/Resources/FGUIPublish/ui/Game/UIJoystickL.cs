/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIJoystickL : GComponent
    {
        public UIctrComp m_ctrl;
        public const string URL = "ui://6laopsuclt3lni";

        public static UIJoystickL CreateInstance()
        {
            return (UIJoystickL)UIPackage.CreateObject("Game", "JoystickL");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_ctrl = (UIctrComp)GetChildAt(0);
        }
    }
}