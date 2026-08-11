using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>Ship classification types.</summary>
    public enum ShipClass
    {
        Scout,          // 轻型侦察舰
        Cruiser,        // 中型巡航舰
        BattleCruiser,  // 重型战列巡洋舰
        Carrier         // 旗舰级航母舰
    }

    /// <summary>Flight control mode.</summary>
    public enum FlightMode
    {
        Normal,   // 标准航行
        Combat,   // 战斗机动
        Warp      // 曲速航行
    }

    /// <summary>RCS (Reaction Control System) mode — Orbiter 2016 style.</summary>
    public enum RCSMode
    {
        ROT,  // 旋转模式: W/S=pitch, A/D=roll, Q/E=yaw — angular acceleration with inertia
        LIN   // 平移模式: W/S=up/down, A/D=left/right, P/L=fwd/back — linear thrust, orientation unchanged
    }

    /// <summary>Camera view mode.</summary>
    public enum CameraMode
    {
        ThirdPerson,  // 第三人称跟随
        Tactical,     // 战术上帝视角
        Bridge        // 舰桥室内视角
    }

    /// <summary>Camera follow mode for ThirdPerson.</summary>
    public enum FollowMode
    {
        Rigid,  // 摄像机直接同步飞船旋转
        Soft    // 摄像机允许最大偏移角度内不同步旋转
    }

    /// <summary>Flight control input mode.</summary>
    public enum ControlMode
    {
        Simple,     // 简易模式 — current arcade-style controls
        Realistic   // 真实模式 — Orbiter-style realistic RCS/thruster physics
    }

    /// <summary>Faction affiliation.</summary>
    public enum Faction
    {
        Player,
        Ally,
        Enemy,
        Neutral
    }

    /// <summary>Weapon types.</summary>
    public enum WeaponType
    {
        Phaser,         // 相位炮
        PhotonTorpedo,  // 光子鱼雷
        IonPulse        // 离子脉冲
    }

    /// <summary>Ship module types for damage system.</summary>
    public enum ModuleType
    {
        Engine,
        Weapon,
        ShieldGenerator,
        Hull,
        Bridge
    }

    /// <summary>Damage type for different weapons.</summary>
    public enum DamageType
    {
        Energy,    // 相位炮能量伤害
        Explosive, // 鱼雷爆炸伤害
        Ion,       // 离子脉冲干扰伤害
        Kinetic    // 动能伤害(碎片)
    }

    /// <summary>AI difficulty levels.</summary>
    public enum AIDifficulty
    {
        Easy,
        Normal,
        Hard,
        Epic
    }

    /// <summary>AI tactical state.</summary>
    public enum AIState
    {
        Idle,
        Patrol,
        Engage,
        Retreat,
        Follow,
        Regroup
    }

    /// <summary>Lock-on targeting mode.</summary>
    public enum LockMode
    {
        WideArea,   // 大范围锁定 - 锁定圆圈内所有敌舰, 总功率分散
        Precision   // 精准锁定 - 锁定少量目标, 集中火力
    }

    /// <summary>Quality preset.</summary>
    public enum QualityPreset
    {
        Low,
        Medium,
        High,
        Ultra
    }
}
