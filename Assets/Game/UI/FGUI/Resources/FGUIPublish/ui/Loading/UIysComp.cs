/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UIysComp : GComponent
    {
        public Controller m_c1;
        public GTextField m_content;
        public const string URL = "ui://69318iqyn7w1p";

        public static UIysComp CreateInstance()
        {
            return (UIysComp)UIPackage.CreateObject("Loading", "ysComp");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_content = (GTextField)GetChildAt(0);
        }
    }
}