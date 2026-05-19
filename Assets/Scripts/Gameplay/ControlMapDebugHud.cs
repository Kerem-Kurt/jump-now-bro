using UnityEngine;
using TMPro;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class ControlMapDebugHud : MonoBehaviour
    {
        [SerializeField] TMP_Text moveLabel;
        [SerializeField] TMP_Text jumpLabel;
        [SerializeField] TMP_Text dashLabel;

        void Start()
        {
            if (ControlMapStore.Instance == null) return;
            ControlMapStore.Instance.OnChanged += UpdateLabels;
            UpdateLabels(ControlMapStore.Instance.Current);
        }

        void OnDestroy()
        {
            if (ControlMapStore.Instance != null)
                ControlMapStore.Instance.OnChanged -= UpdateLabels;
        }

        void UpdateLabels(ControlMap map)
        {
            if (moveLabel != null) moveLabel.text = $"{map.moveOwner}: MOVE";
            if (jumpLabel != null) jumpLabel.text = $"{map.jumpOwner}: JUMP";
            if (dashLabel != null) dashLabel.text = $"{map.dashOwner}: DASH";
        }
    }
}
