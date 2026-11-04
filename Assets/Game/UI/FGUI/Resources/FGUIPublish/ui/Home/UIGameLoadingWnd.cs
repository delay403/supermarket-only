/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIGameLoadingWnd : GComponent
    {
        public GProgressBar m_bar;
        public GTextField m_tf;
        public Transition m_loading;
        public const string URL = "ui://l851vxyyfggke9";

        public static UIGameLoadingWnd CreateInstance()
        {
            return (UIGameLoadingWnd)UIPackage.CreateObject("Home", "GameLoadingWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_bar = (GProgressBar)GetChildAt(1);
            m_tf = (GTextField)GetChildAt(2);
            m_loading = GetTransitionAt(0);
        }
    }
}