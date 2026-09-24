using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(SomethingDownThere.Editor.WorksiteToolsValidator))]
namespace SomethingDownThere.Editor
{
    public sealed class WorksiteToolsValidator : RootObjectValidator<WorksiteTools>
    {
        protected override void Validate(ValidationResult result)
        {
            if (!Object.Configured) result.AddError("Worksite tools need the player, terrain, lit lamp prefab, three stencils and placement materials.");
        }
    }
}
