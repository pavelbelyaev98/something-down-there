using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(SomethingDownThere.Editor.SalvageWinchValidator))]
namespace SomethingDownThere.Editor
{
    public sealed class SalvageWinchValidator : RootObjectValidator<SalvageWinch>
    {
        protected override void Validate(ValidationResult result)
        {
            if(!Object.Configured) result.AddError("Unique recovery needs its terrain, population, player, settings, rope, pad and display references.");
        }
    }
}
