using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 테스트용 재화 추가 메뉴 (플레이 중에만 동작). 게임 화면 왼쪽 위 DEV 패널과 같다.
    public static class DevCheats
    {
        static bool Ready => Application.isPlaying && Game.I != null;

        static void Add(System.Action<SaveData> f)
        {
            if (!Ready) { Debug.LogWarning("[DevCheats] 플레이 중에만 쓸 수 있습니다."); return; }
            f(Game.I.S);
            Game.I.MarkDirty();
        }

        [MenuItem("Mawang/Cheat/Gold +100,000 %#1")] static void Gold() => Add(s => s.gold += 100000);
        [MenuItem("Mawang/Cheat/Materials +100,000 %#2")] static void Mat() => Add(s => s.material += 100000);
        [MenuItem("Mawang/Cheat/Gems +100 %#3")] static void Gems() => Add(s => s.rp += 100);
        [MenuItem("Mawang/Cheat/All ×10 (1M / 1M / 1,000)")] static void Lots() => Add(s => { s.gold += 1000000; s.material += 1000000; s.rp += 1000; });

        [MenuItem("Mawang/Cheat/Gold +100,000 %#1", true)]
        [MenuItem("Mawang/Cheat/Materials +100,000 %#2", true)]
        [MenuItem("Mawang/Cheat/Gems +100 %#3", true)]
        [MenuItem("Mawang/Cheat/All ×10 (1M / 1M / 1,000)", true)]
        static bool Validate() => Ready;
    }
}
