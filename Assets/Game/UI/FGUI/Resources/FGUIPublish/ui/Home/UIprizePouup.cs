/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIprizePouup : GComponent
    {
        public Controller m_c1;
        public GButton m_btn;
        public GButton m_fanhui;
        public const string URL = "ui://l851vxyyrge0mx";

        public static UIprizePouup CreateInstance()
        {
            return (UIprizePouup)UIPackage.CreateObject("Home", "prizePouup");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_btn = (GButton)GetChildAt(3);
            m_fanhui = (GButton)GetChildAt(4);
        }
    }
}