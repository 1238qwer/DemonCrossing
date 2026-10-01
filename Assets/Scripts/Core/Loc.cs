using UnityEngine;

namespace Mawang
{
    // 다국어(한국어 / English). 문자열은 코드 안에서 L.T("한국어", "English") 로 짝지어 쓴다.
    // 언어를 바꾸면 저장 후 장면을 다시 불러 모든 UI를 새 언어로 만든다(GameUI.SetLanguage).
    public static class L
    {
        const string Key = "lang";
        static int lang = -1;

        // 0 = 한국어, 1 = English. 처음에는 기기 언어를 따른다.
        public static int Lang
        {
            get
            {
                if (lang < 0)
                {
                    try { lang = PlayerPrefs.GetInt(Key, Application.systemLanguage == SystemLanguage.Korean ? 0 : 1); }
                    catch { lang = 0; }
                }
                return lang;
            }
            set
            {
                lang = Mathf.Clamp(value, 0, 1);
                PlayerPrefs.SetInt(Key, lang);
                PlayerPrefs.Save();
                UITextSettings.ClearCache();
            }
        }

        public static bool En => Lang == 1;

        // Resources/UIText.asset 에서 고친 문구가 있으면 그것을 쓴다(메뉴 Mawang/UI Text)
        public static string T(string ko, string en) => UITextSettings.Resolve(ko, en) ?? (En ? en : ko);

        // 재화 이름
        public static string Gold => T("골드", "Gold");
        public static string Mat => T("자재", "Materials");
        public static string Gem => T("보석", "Gems");
        public static string Cur(Currency c) => c == Currency.Gold ? Gold : Mat;
    }
}
