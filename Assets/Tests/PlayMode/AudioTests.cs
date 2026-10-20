using System.Collections;
using System.Collections.Generic;
using MotorAndSpeed.Audio;
using MotorAndSpeed.Game;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 音效：玩家引擎聲與喇叭、碰撞聲、NPC 的 3D 引擎聲、環境底噪與比賽倒數。
    /// </summary>
    public class AudioTests : InputTestFixture
    {
        private Keyboard _keyboard;

        [UnityTest]
        public IEnumerator Player_EngineLoopsAndRevsUpWithThrottle()
        {
            yield return Load("WuguWorld");
            PlayerScooterAudio audio = Object.FindFirstObjectByType<PlayerScooterAudio>();
            Assert.IsNotNull(audio, "玩家機車要有聲音");
            Assert.IsNotNull(audio.Engine.clip, "要有引擎聲音效");
            Assert.IsTrue(audio.Engine.loop, "引擎聲要循環");

            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(audio.Engine.isPlaying, "引擎聲要一直播放");
            float idlePitch = audio.Engine.pitch;
            float idleVolume = audio.Engine.volume;

            Press(_keyboard.wKey);
            yield return new WaitForSeconds(2.5f);

            Assert.Greater(audio.Engine.pitch, idlePitch + 0.3f, "催油門加速後轉速（音高）要拉高");
            Assert.Greater(audio.Engine.volume, idleVolume, "催油門加速後要比怠速大聲");
        }

        [UnityTest]
        public IEnumerator Player_EngineStartsSilentAndFadesIn()
        {
            yield return Load("WuguWorld");
            PlayerScooterAudio original = Object.FindFirstObjectByType<PlayerScooterAudio>();

            // 複製一台：Awake、OnEnable 在 Instantiate 當下就跑完，還沒有任何 Update（遊戲啟動第一幀卡住時就是這個狀態）
            GameObject clone = Object.Instantiate(original.gameObject);
            PlayerScooterAudio audio = clone.GetComponent<PlayerScooterAudio>();
            Assert.IsTrue(audio.Engine.isPlaying, "引擎聲一開始就要播放");
            Assert.LessOrEqual(audio.Engine.volume, 0.01f, "引擎聲開播時要從無聲開始，不能用預設音量 1 爆音");
            Object.Destroy(clone);
        }

        [UnityTest]
        public IEnumerator Player_HornPlaysWhileHoldingH()
        {
            yield return Load("WuguWorld");
            PlayerScooterAudio audio = Object.FindFirstObjectByType<PlayerScooterAudio>();
            Assert.IsFalse(audio.Horn.isPlaying);

            Press(_keyboard.hKey);
            yield return null;
            yield return null;
            Assert.IsTrue(audio.Horn.isPlaying, "按住 H 要按喇叭");

            Release(_keyboard.hKey);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(audio.Horn.isPlaying, "放開 H 喇叭要停");
        }

        [UnityTest]
        public IEnumerator Player_CrashIntoWall_PlaysCrashSound()
        {
            yield return Load("Prototype");
            ScooterController scooter = Object.FindFirstObjectByType<ScooterController>();
            PlayerScooterAudio audio = scooter.GetComponent<PlayerScooterAudio>();
            bool impacted = false;
            scooter.Impacted += _ => impacted = true;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(scooter.transform.position + scooter.transform.forward * 10f + Vector3.up * 1.5f, scooter.transform.rotation);
            wall.transform.localScale = new Vector3(8f, 3f, 1f);

            Press(_keyboard.wKey);
            for (float elapsed = 0f; elapsed < 5f && !impacted; elapsed += Time.deltaTime)
            {
                yield return null;
            }

            Assert.IsTrue(impacted, "應該要撞到牆");
            yield return null;
            Assert.IsTrue(audio.Effects.isPlaying, "撞擊要播放碰撞聲");
        }

        [UnityTest]
        public IEnumerator NpcVehicles_Play3DEnginesThatDifferByType()
        {
            yield return Load("WuguWorld");
            WorldTraffic traffic = Object.FindFirstObjectByType<WorldTraffic>();
            yield return new WaitForSeconds(2f);

            Dictionary<string, VehicleEngineAudio> byType = new Dictionary<string, VehicleEngineAudio>();
            int moving = 0;
            foreach (WorldVehicle vehicle in traffic.Vehicles)
            {
                VehicleEngineAudio engine = vehicle.GetComponent<VehicleEngineAudio>();
                Assert.IsNotNull(engine, $"{vehicle.name} 要有引擎聲");
                Assert.IsNotNull(engine.Source.clip, $"{vehicle.name} 要有引擎聲音效");
                Assert.IsTrue(engine.Source.isPlaying, $"{vehicle.name} 的引擎聲要在播放");
                Assert.AreEqual(1f, engine.Source.spatialBlend, $"{vehicle.name} 的引擎聲要是 3D 音效");
                Assert.AreEqual(AudioRolloffMode.Custom, engine.Source.rolloffMode, "要有距離衰減");
                byType[vehicle.name.Replace("(Clone)", string.Empty)] = engine;
                if (engine.EstimatedSpeed > 3f)
                    moving++;
            }

            Assert.GreaterOrEqual(moving, 10, "行駛中的車輛要估得出車速（音高才會變）");

            AudioClip scooter = byType["NpcScooter"].Source.clip;
            AudioClip car = byType["NpcCar"].Source.clip;
            AudioClip truck = byType["NpcTruck"].Source.clip;
            AudioClip bus = byType["NpcBus"].Source.clip;
            Assert.AreEqual(4, new HashSet<AudioClip> { scooter, car, truck, bus }.Count, "機車、汽車、大貨車、公車的引擎聲要不一樣");
            Assert.Greater(byType["NpcTruck"].MaxDistance, byType["NpcScooter"].MaxDistance, "大車的聲音要傳得比機車遠");
        }

        [UnityTest]
        public IEnumerator Ambience_PlaysAndRaceCountdownBeeps()
        {
            yield return Load("WuguWorld");
            GameSounds sounds = GameSounds.Instance;
            Assert.IsNotNull(sounds, "場景要有 GameSounds");
            Assert.IsTrue(sounds.Ambience.isPlaying, "街道環境音要播放");
            Assert.IsTrue(sounds.Ambience.loop, "街道環境音要循環");

            GameModeController modes = Object.FindFirstObjectByType<GameModeController>();
            modes.StartMode(GameMode.RaceSprint);
            yield return null;
            Assert.AreEqual(GameSound.CountdownTick, sounds.LastPlayed, "倒數要嗶聲");
            Assert.IsTrue(sounds.Effects.isPlaying);

            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(GameSound.CountdownGo, sounds.LastPlayed, "起跑要有出發音效");
            foreach (RacerAI racer in modes.Racers)
            {
                VehicleEngineAudio engine = racer.GetComponent<VehicleEngineAudio>();
                Assert.IsNotNull(engine, "AI 選手要有引擎聲");
                Assert.IsTrue(engine.Source.isPlaying);
            }
        }

        private IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();

            // 場景載入後才加入虛擬鍵盤，避免裝置在載入過程中失效
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }
    }
}
