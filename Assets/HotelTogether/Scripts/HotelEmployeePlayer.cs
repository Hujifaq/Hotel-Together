using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HotelTogether
{
    /// <summary>Employee selection and avatar lifetime; HotelPlayer retains gameplay/input authority.</summary>
    public sealed class HotelEmployeePlayer : MonoBehaviour
    {
        [Serializable]
        public sealed class Employee
        {
            public string id, title, description;
            public HotelEmployeeRig prefab;
            public Texture2D portrait;
            public Color accent;
        }
        public Employee[] employees;
        public int SelectedIndex { get; private set; }
        public HotelEmployeeRig Avatar { get; private set; }
        public bool Ready => Avatar != null;
        const string Preference = "HotelTogether.EmployeeId";
        HotelPlayer player;
        bool initialized;

        public bool Initialize(HotelPlayer owner)
        {
            player = owner;
            if (initialized) return Ready;
            initialized = true;
            if (employees == null || employees.Length == 0) return false;
            string saved = PlayerPrefs.GetString(Preference, employees[0].id);
            int index = Array.FindIndex(employees, e => e != null && e.id == saved && e.prefab);
            return SelectEmployee(Mathf.Max(0, index), false);
        }

        public bool SelectEmployee(int index, bool persist = true)
        {
            if (!player || employees == null || index < 0 || index >= employees.Length || employees[index] == null || !employees[index].prefab) return false;
            if (player.held || player.cart || player.seated || player.IsReachingForItem)
            {
                player.Notify("Put down your item and stand up before changing employee.");
                return false;
            }
            if (Avatar && SelectedIndex == index) return true;
            if (Avatar) { Avatar.gameObject.SetActive(false); Destroy(Avatar.gameObject); }
            SelectedIndex = index;
            Avatar = Instantiate(employees[index].prefab, transform);
            Avatar.name = "Player employee - " + employees[index].id;
            Avatar.transform.localPosition = Vector3.back * .28f;
            Avatar.transform.localRotation = Quaternion.identity;
            Avatar.Initialize(true);
            player.handSkinColor = Avatar.skinColor;
            if (persist) { PlayerPrefs.SetString(Preference, employees[index].id); PlayerPrefs.Save(); }
            return true;
        }

        public void UpdatePresentation(float dt) { if (Avatar) Avatar.Pose(player, dt); }

        void Update()
        {
            if (!initialized || Cursor.lockState == CursorLockMode.Locked || player.SettingsOpen || Keyboard.current == null || employees == null || employees.Length == 0) return;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) SelectEmployee((SelectedIndex + employees.Length - 1) % employees.Length);
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) SelectEmployee((SelectedIndex + 1) % employees.Length);
        }

        void OnDestroy() { if (Avatar) Destroy(Avatar.gameObject); }
    }
}
