using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Traducir.Core.Helpers;
using Traducir.Core.Models;
using Traducir.Core.Models.Enums;
using Traducir.Core.Services;
using Traducir.ViewModels.Strings;

namespace Traducir.Controllers
{
    public class StringsController : Controller
    {
        private ISOStringService _soStringService { get; set; }

        public StringsController(ISOStringService soStringService)
        {
            _soStringService = soStringService;
        }

        [HttpPost]
        [Route("app/api/strings/query")]
        public async Task<IActionResult> Query([FromBody] QueryViewModel model)
        {
            Func<SOString, bool> predicate = s => true;
            if (model.WithoutTranslation.HasValue)
            {
                predicate = s => s.Translation.IsNullOrEmpty()== model.WithoutTranslation.Value;
            }
            if (model.SuggestionsStatus != QueryViewModel.SuggestionApprovalStatuses.AnyStatus)
            {
                var oldPredicate = predicate;
                switch (model.SuggestionsStatus)
                {
                    case QueryViewModel.SuggestionApprovalStatuses.DoesNotHaveSuggestionsNeedingApproval:
                        predicate = s => oldPredicate(s)&&
                            (s.Suggestions == null ||
                            !s.Suggestions.Any(sug => sug.State == StringSuggestionState.Created || sug.State == StringSuggestionState.ApprovedByTrustedUser));
                        break;
                    case QueryViewModel.SuggestionApprovalStatuses.HasSuggestionsNeedingApproval:
                        predicate = s => oldPredicate(s)&&
                            (s.Suggestions != null &&
                            s.Suggestions.Any(sug => sug.State == StringSuggestionState.Created || sug.State == StringSuggestionState.ApprovedByTrustedUser));
                        break;
                    case QueryViewModel.SuggestionApprovalStatuses.HasSuggestionsNeedingApprovalApprovedByTrustedUser:
                        predicate = s => oldPredicate(s)&&
                            (s.Suggestions != null &&
                            s.Suggestions.Any(sug => sug.State == StringSuggestionState.ApprovedByTrustedUser));
                        break;
                }
            }
            if (model.SourceRegex.HasValue())
            {
                var oldPredicate = predicate;

                var regex = new Regex(model.SourceRegex, RegexOptions.Compiled);
                predicate = s => oldPredicate(s)&& regex.IsMatch(s.OriginalString);
            }
            if (model.TranslationRegex.HasValue())
            {
                var oldPredicate = predicate;

                var regex = new Regex(model.TranslationRegex, RegexOptions.Compiled);
                predicate = s => oldPredicate(s)&& (s.Translation.HasValue()&& regex.IsMatch(s.Translation));
            }

            return Json(await _soStringService.GetStringsAsync(predicate));
        }

        [HttpPut]
        [Authorize(Policy = "CanSuggest")]
        [Route("app/api/suggestions")]
        public async Task<IActionResult> CreateSuggestion([FromBody] CreateSuggestionViewModel model)
        {
            var success = await _soStringService.CreateSuggestionAsync(model.StringId, model.Suggestion, User.GetClaim<int>(ClaimType.Id));
            if (success)
            {
                return new EmptyResult();
            }
            return BadRequest();
        }

        [HttpPut]
        [Authorize(Policy = "CanReview")]
        [Route("app/api/review")]
        public async Task<IActionResult> Review([FromBody] ReviewViewModel model)
        {
            if (!model.SuggestionId.HasValue || !model.Approve.HasValue)
            {
                return BadRequest();
            }
            var success = await _soStringService.ReviewSuggestionAsync(model.SuggestionId.Value, model.Approve.Value,
                User.GetClaim<int>(ClaimType.Id),
                User.GetClaim<UserType>(ClaimType.UserType));
            if (success)
            {
                return new EmptyResult();
            }
            return BadRequest();
        }
    }
}