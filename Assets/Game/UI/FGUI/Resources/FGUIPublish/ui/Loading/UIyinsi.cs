/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UIyinsi : GComponent
    {
        public Controller m_c1;
        public Controller m_c2;
        public UIysComp m_ys;
        public UIokBtn m_Btnok;
        public UInoBtn m_Btnno;
        public UIokBtn m_OkBtn;
        public const string URL = "ui://69318iqyby9mj";

        public static UIyinsi CreateInstance()
        {
            return (UIyinsi)UIPackage.CreateObject("Loading", "yinsi");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_c2 = GetControllerAt(1);
            m_ys = (UIysComp)GetChildAt(2);
            m_Btnok = (UIokBtn)GetChildAt(3);
            m_Btnno = (UInoBtn)GetChildAt(4);
            m_OkBtn = (UIokBtn)GetChildAt(6);
        }
    }
}