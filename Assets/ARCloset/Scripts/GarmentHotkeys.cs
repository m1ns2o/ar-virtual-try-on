using UnityEngine;

namespace ARCloset
{
    public sealed class GarmentHotkeys : MonoBehaviour
    {
        [SerializeField] private GarmentFittingController fittingController;

        private void Awake()
        {
            if (fittingController == null)
            {
                fittingController = GetComponent<GarmentFittingController>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                fittingController.EquipByIndex(0);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                fittingController.EquipByIndex(1);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                fittingController.EquipByIndex(2);
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                fittingController.EquipByIndex(3);
            }

            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                fittingController.EquipByIndex(4);
            }
        }
    }
}
