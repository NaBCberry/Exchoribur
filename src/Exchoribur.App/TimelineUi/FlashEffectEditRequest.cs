using Exchoribur.Core.Models;

namespace Exchoribur.App.TimelineUi;

public sealed record FlashEffectEditRequest(Guid BlockId, Guid EffectId, FlashEffect? Effect);
