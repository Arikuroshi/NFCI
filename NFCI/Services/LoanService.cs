using NFCI.Models;

namespace NFCI.Services
{
    public sealed class LoanService
    {
        private readonly ITagRepository _tagRepository;

        public LoanService(ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
        }

        public async Task<TagRecord?> FindTagByUidAsync(
            string uid,
            CancellationToken cancellationToken = default)
        {
            return await _tagRepository.FindByUidAsync(
                NormalizeUid(uid),
                cancellationToken);
        }

        public async Task RegisterTagAsync(
            string uid,
            string name,
            CancellationToken cancellationToken = default)
        {
            var normalizedUid = NormalizeUid(uid);
            var normalizedName = name.Trim();

            if (normalizedName.Length == 0)
            {
                throw new ArgumentException("Enter a name for the tag.", nameof(name));
            }

            if (normalizedName.Length > 120)
            {
                throw new ArgumentException(
                    "The name cannot exceed 120 characters.",
                    nameof(name));
            }

            await _tagRepository.SaveAsync(
                new TagRecord(normalizedUid, normalizedName, DateTimeOffset.UtcNow),
                cancellationToken);
        }

        private static string NormalizeUid(string uid)
        {
            var compactUid = uid
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);

            try
            {
                var bytes = Convert.FromHexString(compactUid);

                if (bytes.Length == 0)
                {
                    throw new ArgumentException("Scan a tag before saving it.", nameof(uid));
                }

                return Convert.ToHexString(bytes);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException(
                    "The tag UID must contain hexadecimal characters.",
                    nameof(uid),
                    exception);
            }
        }
    }
}

