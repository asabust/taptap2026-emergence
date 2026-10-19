namespace Game.Runtime.Core
{
    public enum GamePhase
    {
        GameTitle, // 游戏标题界面
        GameOpening, //开场剧情
        Gameplay, // 游戏中
        GameOver, // 游戏结束
        LevelSelect // 人体选关；追加以保持已有枚举的序列化值
    }
}
