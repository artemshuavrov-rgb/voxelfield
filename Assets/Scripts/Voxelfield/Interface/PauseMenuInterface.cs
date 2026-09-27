using Swihoni.Sessions;
using Swihoni.Sessions.Config;
using Swihoni.Sessions.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using Voxelfield.Session;

namespace Voxelfield.Interface
{
    public class PauseMenuInterface : SessionInterfaceBehavior
    {
        private GameObject m_MainPage;
        private GameObject m_ControlsPage;

        protected override void Awake()
        {
            base.Awake();
            DemoUi.HideExistingChildren(transform);
            var layout = GetComponent<LayoutGroup>();
            if (layout) layout.enabled = false;
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter) fitter.enabled = false;
            BuildMenu();
        }

        public override void Render(in SessionContext context)
        {
            if (NoInterrupting && InputProvider.GetInputDown(InputType.TogglePauseMenu))
                ToggleInterfaceActive();
        }

        protected override void OnSetInterfaceActive(bool isActive)
        {
            if (isActive) ShowControls(false);
        }

        private void BuildMenu()
        {
            DemoUi.Image(transform, "Dim world", Vector2.zero, Vector2.one,
                         Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.07f, 0.87f), true);
            DemoUi.Box(transform, "Menu panel", new Vector2(550, 520), Vector2.zero, DemoUi.Background);
            DemoUi.Box(transform, "Top accent", new Vector2(550, 5), new Vector2(0, 257), DemoUi.Accent);

            m_MainPage = DemoUi.Center(transform, "Pause home", new Vector2(550, 520), Vector2.zero).gameObject;
            var home = m_MainPage.transform;
            DemoUi.Text(home, "Overline", "SOLO SANDBOX  /  CASTLE", new Vector2(480, 26),
                        new Vector2(0, 202), 14, DemoUi.Accent, style: TMPro.FontStyles.Bold);
            DemoUi.Text(home, "Heading", "PAUSED", new Vector2(480, 70),
                        new Vector2(0, 144), 52, DemoUi.White, style: TMPro.FontStyles.Bold);
            DemoUi.Text(home, "Hint", "Take a breath. The world will still be here.",
                        new Vector2(480, 42), new Vector2(0, 94), 17, DemoUi.Muted);
            DemoUi.Button(home, "Resume", "RESUME GAME", new Vector2(420, 48),
                          new Vector2(0, 28), true, () => SetInterfaceActive(false));
            DemoUi.Button(home, "Reset arena", "RESET ARENA", new Vector2(420, 48),
                          new Vector2(0, -33), false, () => SessionManager.StartSoloDemo());
            DemoUi.Button(home, "Controls", "CONTROLS", new Vector2(420, 48),
                          new Vector2(0, -94), false, () => ShowControls(true));
            DemoUi.Button(home, "Main menu", "MAIN MENU", new Vector2(205, 44),
                          new Vector2(-107, -166), false, DisconnectButton);
            DemoUi.Button(home, "Quit", "QUIT GAME", new Vector2(205, 44),
                          new Vector2(107, -166), false, QuitButton);

            m_ControlsPage = DemoUi.Center(transform, "Pause controls", new Vector2(550, 520), Vector2.zero).gameObject;
            var controls = m_ControlsPage.transform;
            DemoUi.Text(controls, "Heading", "CONTROLS", new Vector2(480, 65),
                        new Vector2(0, 192), 42, DemoUi.White, style: TMPro.FontStyles.Bold);
            DemoUi.Text(controls, "Controls list",
                        "W A S D     Move             MOUSE     Look / fire\n" +
                        "SHIFT         Sprint             SPACE      Jump\n" +
                        "1 – 0           Weapons         R               Reload\n" +
                        "G                Drop item        ESC           Resume",
                        new Vector2(480, 210), new Vector2(0, 25), 16, DemoUi.White);
            DemoUi.Button(controls, "Back", "BACK", new Vector2(420, 50),
                          new Vector2(0, -175), true, () => ShowControls(false));
            ShowControls(false);
        }

        private void ShowControls(bool show)
        {
            m_MainPage.SetActive(!show);
            m_ControlsPage.SetActive(show);
        }

        public void ConfigurationButton() => ShowControls(true);

        public void QuitButton()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void DisconnectButton() => SessionManager.DisconnectAll();
    }
}
