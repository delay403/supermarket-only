/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UIhwLogin : GComponent
    {
        public GButton m_outBtn;
        public GButton m_loginBtn;
        public const string URL = "ui://69318iqys5wg19";

        public static UIhwLogin CreateInstance()
        {
            return (UIhwLogin)UIPackage.CreateObject("Loading", "hwLogin");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_outBtn = (GButton)GetChildAt(3);
            m_loginBtn = (GButton)GetChildAt(4);
        }
    }
}