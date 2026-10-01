using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class WorldSaveController
    {
        public bool HasSavedGame { get; private set; }
        public bool AwaitingGameChoice => State == WorldSaveState.Startup || State == WorldSaveState.ConfirmNewGame;
        private bool startedFromMenu, loadMustExist, replaceExisting;

        public void PresentStartup(string directory)
        {
            OwnSession(directory);
            startedFromMenu = true;
            RefreshStartup();
        }

        public void RefreshStartup()
        {
            if (initialized || read != null || write != null || State == WorldSaveState.Creating) return;
            store?.Dispose();
            store = null;
            HasSavedGame = WorldSaveStore.HasCheckpoint(SaveDirectory);
            SetState(WorldSaveState.Startup);
            player.ShowSessionMenu(PlayerMenu.MainMenu);
        }

        public void LoadGame()
        {
            if (State != WorldSaveState.Startup || player.Menu != PlayerMenu.MainMenu) return;
            HasSavedGame = WorldSaveStore.HasCheckpoint(SaveDirectory);
            if (!HasSavedGame) { Changed?.Invoke(); return; }
            loadMustExist = true;
            StartCoroutine(Load());
        }

        public void RequestNewGame()
        {
            if (State != WorldSaveState.Startup || player.Menu != PlayerMenu.MainMenu) return;
            HasSavedGame = WorldSaveStore.HasCheckpoint(SaveDirectory);
            replaceExisting = false;
            if (HasSavedGame)
            {
                SetState(WorldSaveState.ConfirmNewGame);
                player.ShowSessionMenu(PlayerMenu.ConfirmNewGame);
            }
            else StartCoroutine(CreateNewGame());
        }

        // Developer Ground Lab: the bays and crane scenes instead of the site, never saved (no store,
        // never initialized, so nothing is captured or written); leaving reloads MainGame, restarting
        // reloads it straight back into the lab.
        private static bool restartLab;

        public void StartGroundLab()
        {
            if (!FpsPlayer.AdminBuild || State != WorldSaveState.Startup || player.Menu != PlayerMenu.MainMenu || terrain == null) return;
            StartCoroutine(OpenGroundLab());
        }

        private IEnumerator OpenGroundLab()
        {
            SetState(WorldSaveState.Creating);
            player.ShowPersistenceMenu();
            yield return null;
            terrain.UseGroundLab();
            if (discoveries != null && discoveries.Catalog != null) discoveries.UseGroundLab();
            SetState(WorldSaveState.Lab);
            player.CloseMenu();
            player.BeginGroundLab();
        }

        public void LeaveGroundLab()
        {
            if (State != WorldSaveState.Lab) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        public void RestartGroundLab()
        {
            if (State != WorldSaveState.Lab) return;
            restartLab = true;
            LeaveGroundLab();
        }

        // After a restart the reloaded title goes straight back into the lab.
        private void ResumeRestartedLab()
        {
            if (!restartLab || State != WorldSaveState.Startup || player.Menu != PlayerMenu.MainMenu || player.GraphicsTuner.Running) return;
            restartLab = false;
            StartGroundLab();
        }

        public void CancelNewGame()
        {
            if (State == WorldSaveState.ConfirmNewGame) RefreshStartup();
        }

        public void ConfirmNewGame()
        {
            if (State != WorldSaveState.ConfirmNewGame || player.Menu != PlayerMenu.ConfirmNewGame) return;
            replaceExisting = true;
            StartCoroutine(CreateNewGame());
        }

        private IEnumerator CreateNewGame()
        {
            SetState(WorldSaveState.Creating);
            player.ShowPersistenceMenu();
            yield return null;
            Exception failure = null;
            try
            {
                WorldSnapshot.Require(terrain != null && discoveries != null, "The excavation scene is incomplete.");
                discoveries.InitializePopulation();
                WorldSnapshot.Require(discoveries.Initialized, "The discovery population could not be initialized.");
                writing = Capture(1);
                store?.Dispose();
                store = new WorldSaveStore(SaveDirectory);
                var snapshot = writing;
                write = Task.Run(() => store.ReplaceWithNewGame(snapshot, replaceExisting));
            }
            catch (Exception error) { failure = error; }
            if (failure == null)
            {
                while (!write.IsCompleted) yield return null;
                if (write.IsFaulted) failure = write.Exception.Flatten().InnerExceptions[0];
                write = null;
            }
            if (failure != null)
            {
                DescribeFailure(failure);
                Debug.LogWarning("New game: " + failure);
                exitRequested = false;
                SetState(WorldSaveState.NewGameFailed);
                yield break;
            }
            CompletedSequence = nextSequence = writing.Sequence;
            LastCommitMetrics = store.LastCommitMetrics;
            LastSavedLabel = "Saved " + new DateTime(writing.UtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss");
            writing = null;
            observed = Observe();
            dirtyVersion = capturedVersion = savedVersion = 0;
            dirtySince = Time.realtimeSinceStartupAsDouble;
            initialized = true;
            HasSavedGame = true;
            FinishLoading(false);
            if (exitRequested) QuitNow();
        }
    }
}
