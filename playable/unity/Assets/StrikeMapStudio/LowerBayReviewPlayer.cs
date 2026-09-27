using System;
using UnityEngine;

namespace StrikeMapStudio
{
    // A standalone map walkthrough. This deliberately does not impersonate the
    // original game's combat, authenticated item catalog, bots or network code.
    [RequireComponent(typeof(CharacterController))]
    public sealed class LowerBayReviewPlayer : MonoBehaviour
    {
        public Camera view;
        public StrikeMapTrain train;
        public Transform spawn;
        public TextAsset source;
        public bool acceptingInput = true;
        public int collected;
        private float verticalSpeed, pitch;
        private bool menu = true;
        private GUIStyle title, copy, button;
        public CharacterController Controller { get; private set; }

        private void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Application.targetFrameRate = 120;
        }

        private void Update()
        {
            if (!acceptingInput) return;
            if (Input.GetKeyDown(KeyCode.Escape)) SetMenu(!menu);
            if (menu) return;
            if (Input.GetKeyDown(KeyCode.R)) Respawn();
            if (Input.GetKeyDown(KeyCode.T)) train.Paused = !train.Paused;
            if (Input.GetKeyDown(KeyCode.Home)) { train.Restart(); Respawn(); }
            transform.Rotate(0, Input.GetAxisRaw("Mouse X") * 1.7f, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.7f, -85, 85);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector2 keys = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            Vector3 heading = transform.TransformDirection(new Vector3(keys.x, 0, keys.y));
            Drive(new Vector2(heading.x, heading.z), Input.GetKeyDown(KeyCode.Space), Time.deltaTime, Input.GetKey(KeyCode.LeftShift));
            if (transform.position.y < -8) Respawn();
        }

        public void Drive(Vector2 direction, bool jump, float dt, bool sprint = false)
        {
            dt = Mathf.Clamp(dt, 0, .05f);
            if (Controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (jump && Controller.isGrounded) verticalSpeed = 6;
            verticalSpeed -= 18 * dt;
            direction = Vector2.ClampMagnitude(direction, 1) * (sprint ? 8 : 5);
            Controller.Move(new Vector3(direction.x, verticalSpeed, direction.y) * dt);
        }

        public void Place(Vector3 feet, float yaw = 90)
        {
            Controller.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0, yaw, 0));
            Controller.enabled = true;
            verticalSpeed = 0;
            Physics.SyncTransforms();
        }

        public void Respawn() { Place(spawn.position, spawn.eulerAngles.y); }
        public void SetMenu(bool open)
        {
            menu = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private void OnGUI()
        {
            if (!acceptingInput) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
                copy = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 19 };
            }
            if (menu)
            {
                float x = Mathf.Max(18, (Screen.width - 610) / 2f), y = Mathf.Max(18, (Screen.height - 410) / 2f);
                GUI.Box(new Rect(x - 22, y - 20, 654, 422), "");
                GUI.Label(new Rect(x, y, 610, 55), "LOWER BAY", title);
                GUI.Label(new Rect(x, y + 58, 610, 70), "Explore the reconstructed station, upper rooms and track. Drop from the catwalks to collect the sniper marker. Leave the roof before the train carries you into an overhead platform.", copy);
                GUI.Label(new Rect(x, y + 145, 610, 100), "WASD  Move     Mouse  Look     Space  Jump\nShift  Run     R  Respawn     T  Pause train\nHome  Reset train and player     Esc  Menu", copy);
                GUI.Label(new Rect(x, y + 250, 610, 55), "Reconstruction review build · inferred dimensions and train timing. Pickup markers are illustrative. Original-game combat and multiplayer are separate.", copy);
                if (GUI.Button(new Rect(x, y + 326, 280, 48), "Walk through", button)) SetMenu(false);
                if (GUI.Button(new Rect(x + 300, y + 326, 280, 48), "Quit", button)) Application.Quit();
            }
            else
            {
                GUI.Box(new Rect(12, 12, 425, 58), "");
                GUI.Label(new Rect(25, 18, 405, 48), "LOWER BAY  /  " + collected + " pickups\nEsc Menu    R Respawn    T Train " + (train.Paused ? "paused" : "moving"), copy);
                GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 24), "+");
            }
        }
    }
}
