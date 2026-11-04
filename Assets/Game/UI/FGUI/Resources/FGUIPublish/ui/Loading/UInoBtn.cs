/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UInoBtn : GButton
    {
        public GImage m_Btnno;
        public const string URL = "ui://69318iqyn7w1r";

        public static UInoBtn CreateInstance()
        {
            return (UInoBtn)UIPackage.CreateObject("Loading", "noBtn");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Btnno = (GImage)GetChildAt(0);
        }
    }
}