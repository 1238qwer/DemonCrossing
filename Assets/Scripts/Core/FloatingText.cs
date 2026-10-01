using System;
using UnityEngine;

namespace Mawang
{
    // 시뮬레이션 → 뷰로 떠오르는 텍스트(+골드 등)를 전달하는 통로
    public static class FloatingText
    {
        public static event Action<Vector2, string> Emitted;
        public static void Emit(Vector2 castlePos, string text) => Emitted?.Invoke(castlePos, text);
    }
}
