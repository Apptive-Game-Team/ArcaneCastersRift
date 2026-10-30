using UnityEngine;
namespace ArcaneCasters.GameSystem {
    public sealed class RuntimeMeshOwner : MonoBehaviour {
        public Mesh Owned;
        private void OnDestroy(){if(Owned)Destroy(Owned);}
    }
}
