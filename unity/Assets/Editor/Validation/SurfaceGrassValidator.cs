using Sirenix.OdinInspector.Editor.Validation;
using UnityEditor;
using UnityEngine;

[assembly: RegisterValidator(typeof(SomethingDownThere.Editor.SurfaceGrassValidator))]

namespace SomethingDownThere.Editor
{
    // A material reimport must not silently remove excavation clipping from a species.
    public sealed class SurfaceGrassValidator : RootObjectValidator<SurfaceGrassRenderer>
    {
        protected override void Validate(ValidationResult result)
        {
            using var settings = new SerializedObject(Object);
            var layers = settings.FindProperty("detailLayers");
            if (layers.arraySize == 0)
            {
                Check(settings.FindProperty("nearMesh"), settings.FindProperty("material"), "Grass", result);
                return;
            }
            for (int i = 0; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                Check(layer.FindPropertyRelative("mesh"), layer.FindPropertyRelative("material"), $"Meadow layer {i + 1}", result);
            }
        }

        private static void Check(SerializedProperty mesh, SerializedProperty material, string label, ValidationResult result)
        {
            if (mesh.objectReferenceValue == null) result.AddError($"{label} needs a plant mesh.");
            if (material.objectReferenceValue is not Material value)
                result.AddError($"{label} needs a material.");
            else if (!value.HasProperty("_ExcavationGrassClip"))
                result.AddError($"{label} material '{value.name}' does not support excavation clipping. Use the project Excavation Grass shader.");
        }
    }
}
