/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UIokBtn : GButton
    {
        public GImage m_Btnok;
        public const string URL = "ui://69318iqyn7w1q";

        public static UIokBtn CreateInstance()
        {
            return (UIokBtn)UIPackage.CreateObject("Loading", "okBtn");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Btnok = (GImage)GetChildAt(0);
        }
    }
}