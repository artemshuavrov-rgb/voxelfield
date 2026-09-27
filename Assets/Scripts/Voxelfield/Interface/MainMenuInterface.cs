using Swihoni.Sessions;
using Swihoni.Sessions.Config;
using Swihoni.Sessions.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Voxelfield.Session;

namespace Voxelfield.Interface
{
    public class MainMenuInterface : SessionInterfaceBehavior
    {
        private GameObject m_HomePage;
        private GameObject m_ControlsPage;
        private Button m_StartButton;

        protected override void Awake()
        {
            base.Awake();
            Config.Active.showDebugInterface.Value = false;
            var fps = FindFirstObjectByType<FpsInterface>();
            if (fps) fps.gameObject.SetActive(false);
            var version = FindFirstObjectByType<VersionInterface>();
            if (version) version.gameObject.SetActive(false);
            DemoUi.HideExistingChildren(transform);
            BuildMenu();
        }

        public override void Render(in SessionContext context) { }

        protected override void OnSetInterfaceActive(bool isActive)
        {
            if (isActive) ShowControls(false);
            if (isActive && EventSystem.current && m_StartButton)
                EventSystem.current.SetSelectedGameObject(m_StartButton.gameObject);
            else if (!isActive && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void BuildMenu()
        {
            DemoUi.Image(transform, "Dark backdrop", Vector2.zero, Vector2.one,
                         Vector2.zero, Vector2.zero, DemoUi.Background, true);
            DemoUi.Image(transform, "Left accent", new Vector2(0, 0), new Vector2(0, 1),
                         new Vector2(7, 0), new Vector2(3, 0), DemoUi.Accent);

            // Broad shapes echo the game's voxel terrain without obscuring the controls.
            DemoUi.Box(transform, "Voxel backdrop A", new Vector2(180, 180), new Vector2(325, 185),
                       new Color(0.12f, 0.27f, 0.30f, 0.24f));
            DemoUi.Box(transform, "Voxel backdrop B", new Vector2(130, 130), new Vector2(242, -118),
                       new Color(0.15f, 0.37f, 0.38f, 0.22f));
            DemoUi.Box(transform, "Voxel backdrop C", new Vector2(72, 72), new Vector2(355, -195),
                       new Color(0.38f, 0.91f, 0.76f, 0.12f));

            m_HomePage = DemoUi.Center(transform, "Home", new Vector2(680, 530), Vector2.zero).gameObject;
            var home = m_HomePage.transform;
            DemoUi.Text(home, "Eyebrow", "SOLO SANDBOX  /  CASTLE", new Vector2(600, 28),
                        new Vector2(0, 215), 15, DemoUi.Accent, style: TMPro.FontStyles.Bold);
            DemoUi.Text(home, "Title", "VOXELFIELD", new Vector2(620, 84),
                        new Vector2(0, 148), 65, DemoUi.White, style: TMPro.FontStyles.Bold);
            DemoUi.Box(home, "Rule", new Vector2(74, 4), new Vector2(-264, 89), DemoUi.Accent);
            DemoUi.Text(home, "Tagline", "BREAK THE WORLD.", new Vector2(600, 50),
                        new Vector2(0, 50), 27, DemoUi.White, style: TMPro.FontStyles.Bold);
            DemoUi.Text(home, "Description", "Explore the arena with the full arsenal. Use the pickaxe and explosives to carve a path through destructible voxel terrain.",
                        new Vector2(600, 76), new Vector2(0, -6), 18, DemoUi.Muted);
            m_StartButton = DemoUi.Button(home, "Start demo", "START SOLO DEMO", new Vector2(288, 54),
                                          new Vector2(-156, -91), true, StartDemo);
            DemoUi.Button(home, "How to play", "HOW TO PLAY", new Vector2(288, 54),
                          new Vector2(156, -91), false, () => ShowControls(true));
            DemoUi.Text(home, "Quick controls", "WASD  MOVE    •    MOUSE  AIM / FIRE    •    1–0  WEAPONS    •    ESC  PAUSE",
                        new Vector2(620, 40), new Vector2(0, -168), 14, DemoUi.Muted,
                        TMPro.TextAlignmentOptions.Center);
            DemoUi.Button(home, "Quit", "QUIT", new Vector2(128, 38),
                          new Vector2(0, -225), false, Quit);

            m_ControlsPage = DemoUi.Center(transform, "Controls", new Vector2(680, 530), Vector2.zero).gameObject;
            var controls = m_ControlsPage.transform;
            DemoUi.Text(controls, "Title", "HOW TO PLAY", new Vector2(620, 64),
                        new Vector2(0, 196), 44, DemoUi.White, style: TMPro.FontStyles.Bold);
            DemoUi.Text(controls, "Introduction", "The Castle is yours to explore. The rifle is ready at spawn; select the pickaxe or explosives to destroy terrain.",
                        new Vector2(620, 78), new Vector2(0, 122), 18, DemoUi.Muted);
            DemoUi.Text(controls, "Controls list",
                        "W A S D       Move                    MOUSE       Look / fire\n" +
                        "SHIFT           Sprint                    SPACE          Jump\n" +
                        "1 – 0             Switch weapons       R                 Reload\n" +
                        "G                  Drop item               ESC            Pause menu",
                        new Vector2(620, 196), new Vector2(0, -29), 17, DemoUi.White);
            DemoUi.Button(controls, "Back", "BACK", new Vector2(288, 52),
                          new Vector2(-156, -210), false, () => ShowControls(false));
            DemoUi.Button(controls, "Start demo", "START SOLO DEMO", new Vector2(288, 52),
                          new Vector2(156, -210), true, StartDemo);
            ShowControls(false);
        }

        private void ShowControls(bool show)
        {
            m_HomePage.SetActive(!show);
            m_ControlsPage.SetActive(show);
        }

        private static void StartDemo()
        {
            if (SessionManager.StartSoloDemo() == null)
                Debug.LogError("Could not start the solo demo. Check the Unity Console for details.");
        }

        // The old serialized button remains compatible with the scene while the new menu is built.
        public void OnPlayButton(Button button) => StartDemo();

        public void OnSettingsButton() => ShowControls(true);

        public void OnQuitButton() => Quit();

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
