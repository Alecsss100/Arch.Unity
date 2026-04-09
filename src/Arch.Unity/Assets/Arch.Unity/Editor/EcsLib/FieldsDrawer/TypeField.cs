using UnityEngine;

namespace L.ECS
{
    /// <summary>
    /// A serializable container for a System.Type, allowing it to be selected in the Unity Inspector.
    /// </summary>
    [System.Serializable]
    public class TypeField
    {
        [SerializeField]
        private string _typeName;

        private System.Type _cachedType;

        public System.Type Type
        {
            get
            {
                if (_cachedType == null && !string.IsNullOrEmpty(_typeName))
                {
                    _cachedType = System.Type.GetType(_typeName);
                }
                return _cachedType;
            }
        }
    }
}