namespace Mawang
{
    // 연구 결과를 각 시스템이 읽는 보정치로 변환한다. 연구가 시스템을 직접 조작하지 않도록 분리.
    public class Modifiers
    {
        public float fishingIntervalMul = 1f;
        public int durabilityAdd;
        public float restTime = GameData.HunterRestTime;
        public float admissionMul = 1f;
        public float tankCapMul = 1f;
        public float walletMul = 1f;
        public int makiPerCellAdd;
        public float trashMul = 1f;
        public float visitorRateMul = 1f;
        public int visitorMaxAdd;
        public float patienceAdd;
        public float salesAdd;
        public float labSpeedMul = 1f;
        public int labCapAdd;

        public void Recalculate(SaveData s)
        {
            int Lv(string line) => Level(s, line);

            fishingIntervalMul = 1f - Lv("speed") * 0.04f;
            durabilityAdd = Lv("dura") * 3;
            restTime = GameData.RestTimeAt(Lv("rest"));
            admissionMul = 1f + Lv("adm") * 0.10f;
            tankCapMul = 1f + Lv("cap") * 0.15f;
            makiPerCellAdd = Lv("maki");
            visitorRateMul = 1f + Lv("visitor") * 0.12f;
            visitorMaxAdd = Lv("visitor") * GameData.VisitorMaxPerLevel;
            walletMul = 1f + Lv("wallet") * 0.20f;
            patienceAdd = Lv("patience") * 1.5f;
            salesAdd = Lv("sales") * 0.05f;
            labSpeedMul = 1f + Lv("lab") * 0.08f;
            labCapAdd = Lv("lab") * 2;
            trashMul = 1f + Lv("trash") * 0.5f;
        }

        // 업그레이드 라인의 현재 단계: 1단계부터 연속으로 완료된 수
        public static int Level(SaveData s, string line)
        {
            int n = 0;
            while (s.research.Contains($"{line}_{n + 1}")) n++;
            return n;
        }

        public static bool RegionUnlocked(SaveData s, RegionDef r) =>
            string.IsNullOrEmpty(r.unlockResearch) || s.research.Contains(r.unlockResearch);
    }
}
