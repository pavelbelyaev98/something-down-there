using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(SomethingDownThere.Editor.SalvageCraneValidator))]
namespace SomethingDownThere.Editor
{
    public sealed class SalvageCraneValidator : RootObjectValidator<SalvageCrane>
    {
        protected override void Validate(ValidationResult result)
        {
            if(!Object.Configured) result.AddError("The salvage crane needs its terrain, population, player, rig, set-down spots and mark material. Run Configure Salvage Crane.");
        }
    }
}
