using UnityEngine;

namespace MonStacka.UI
{
    public sealed class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        public void SetVisible(bool visible)
        {
            if (root)
            {
                root.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
