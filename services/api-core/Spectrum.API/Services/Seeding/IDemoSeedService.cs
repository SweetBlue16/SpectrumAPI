using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Spectrum.API.Configuration;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Seed;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace Spectrum.API.Services.Seeding
{
    public interface IDemoSeedService
    {
        Task<DemoSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
        Task<DemoSeedResultDto> CleanupAsync(CancellationToken cancellationToken = default);
    }

    public class DemoSeedService : IDemoSeedService
    {
        private const string DemoPrefix = "DEMO_";
        private const string DemoDomain = "@demo.spectrum.local";
        private const string StatusPending = "PENDING";
        private const string UserIdField = "userId";

        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;
        private readonly DemoSeedOptions _options;

        public DemoSeedService(
            SpectrumDbContext context,
            IGameRepository gameRepository,
            IOptions<DemoSeedOptions> options
        )
        {
            _context = context;
            _gameRepository = gameRepository;
            _options = options.Value;
        }

        public async Task<DemoSeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
        {
            await CleanupAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var games = await EnsureGamesAsync(cancellationToken);
            var platforms = await _context.Platforms.OrderBy(platform => platform.Id).ToListAsync(cancellationToken);
            var users = BuildDemoUsers(now).ToList();
            var admin = users.Single(user => user.Role == Constants.Roles.Admin);
            var reviewers = users.Where(user => user.Role == Constants.Roles.Reviewer).ToList();

            await _context.Users.AddRangeAsync(users, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var user in reviewers)
            {
                user.Platforms.Add(platforms[(Math.Abs(user.Username.GetHashCode()) % platforms.Count)]);
                user.Platforms.Add(platforms[(Math.Abs(user.Email.GetHashCode()) % platforms.Count)]);
                foreach (var game in games.Skip(Math.Abs(user.Username.GetHashCode()) % 5).Take(4))
                {
                    user.InterestedGames.Add(game);
                }
            }

            await _context.AdminDetails.AddAsync(new AdminDetail
            {
                Id = StableGuid("admin-detail"),
                UserId = admin.Id,
                FirstName = "Demo",
                LastName = "Administrator",
                PhoneNumber = "+5212280000000",
                Address = "DEMO_ Spectrum HQ, Xalapa",
                Rfc = "DEMO990101AB1"
            }, cancellationToken);

            var reviews = BuildReviews(reviewers, games, now).ToList();
            await _context.Reviews.AddRangeAsync(reviews, cancellationToken);
            var clips = BuildClips(reviewers, games, now).ToList();
            await _context.GameClips.AddRangeAsync(clips, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var mongoResult = await SeedMongoAsync(users, reviews, now, cancellationToken);

            return new DemoSeedResultDto
            {
                Admins = 1,
                Users = reviewers.Count,
                Reviews = reviews.Count,
                Clips = clips.Count,
                Comments = mongoResult.Comments,
                Votes = mongoResult.Votes,
                Reports = mongoResult.Reports,
                DropEvents = mongoResult.DropEvents,
                DropParticipants = mongoResult.DropParticipants,
                Message = "Demo seed completed."
            };
        }

        public async Task<DemoSeedResultDto> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var demoUsers = await _context.Users
                .IgnoreQueryFilters()
                .Where(user => user.Email.EndsWith(DemoDomain) || user.Username.StartsWith(DemoPrefix))
                .ToListAsync(cancellationToken);
            var demoUserIds = demoUsers.Select(user => user.Id).ToList();

            var demoReviews = await _context.Reviews
                .IgnoreQueryFilters()
                .Where(review => demoUserIds.Contains(review.UserId) || review.Title.StartsWith(DemoPrefix))
                .ToListAsync(cancellationToken);
            var demoClips = await _context.GameClips
                .IgnoreQueryFilters()
                .Where(clip => demoUserIds.Contains(clip.UserId) || clip.Title.StartsWith(DemoPrefix))
                .ToListAsync(cancellationToken);
            var demoAdminDetails = await _context.AdminDetails
                .Where(detail => demoUserIds.Contains(detail.UserId) || detail.Rfc.StartsWith("DEMO"))
                .ToListAsync(cancellationToken);
            var demoBlocks = await _context.UserBlocks
                .Where(block => demoUserIds.Contains(block.BlockerUserId) || demoUserIds.Contains(block.BlockedUserId))
                .ToListAsync(cancellationToken);
            var demoCodes = await _context.VerificationCodes
                .Where(code => code.Email.EndsWith(DemoDomain))
                .ToListAsync(cancellationToken);

            _context.UserBlocks.RemoveRange(demoBlocks);
            _context.VerificationCodes.RemoveRange(demoCodes);
            _context.GameClips.RemoveRange(demoClips);
            _context.Reviews.RemoveRange(demoReviews);
            _context.AdminDetails.RemoveRange(demoAdminDetails);
            _context.Users.RemoveRange(demoUsers);
            await _context.SaveChangesAsync(cancellationToken);

            var mongoResult = await CleanupMongoAsync(demoUserIds, cancellationToken);

            return new DemoSeedResultDto
            {
                Users = demoUsers.Count(user => user.Role == Constants.Roles.Reviewer),
                Admins = demoUsers.Count(user => user.Role == Constants.Roles.Admin),
                Reviews = demoReviews.Count,
                Clips = demoClips.Count,
                Comments = mongoResult.Comments,
                Votes = mongoResult.Votes,
                Reports = mongoResult.Reports,
                DropEvents = mongoResult.DropEvents,
                DropParticipants = mongoResult.DropParticipants,
                Message = "Demo data cleaned."
            };
        }

        private async Task<IReadOnlyList<Game>> EnsureGamesAsync(CancellationToken cancellationToken)
        {
            var catalogGames = _gameRepository.GetAll()
                .Where(game => game.RawgId > 0)
                .OrderByDescending(game => game.ReleaseDate ?? DateTime.MinValue)
                .Take(18)
                .ToList();

            if (catalogGames.Count < 10)
            {
                catalogGames = BuildFallbackGames().ToList();
            }

            var result = new List<Game>();
            foreach (var source in catalogGames)
            {
                var stableId = GameMappingUtilities.GenerateDeterministicGuid(source.RawgId);
                var game = await _context.Games.FirstOrDefaultAsync(item => item.Id == stableId, cancellationToken);
                if (game is null)
                {
                    game = new Game
                    {
                        Id = stableId,
                        RawgId = source.RawgId,
                        Title = source.Title,
                        Developer = string.IsNullOrWhiteSpace(source.Developer) ? "DEMO_ Studio" : source.Developer,
                        Description = string.IsNullOrWhiteSpace(source.Description) ? $"DEMO_ Catalog entry for {source.Title}" : source.Description,
                        ReleaseDate = source.ReleaseDate,
                        CoverImageUrl = source.CoverImageUrl
                    };
                    await _context.Games.AddAsync(game, cancellationToken);
                }

                result.Add(game);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return result;
        }

        private IEnumerable<User> BuildDemoUsers(DateTime now)
        {
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword(_options.DemoAdminPassword);
            var reviewerPasswordHash = BCrypt.Net.BCrypt.HashPassword(_options.DemoPassword);
            yield return new User
            {
                Id = StableGuid("admin"),
                Username = "DEMO_Admin",
                Email = $"admin{DemoDomain}",
                PasswordHash = adminPasswordHash,
                Role = Constants.Roles.Admin,
                IsEmailVerified = true,
                IsDeleted = false,
                IsSuspended = false,
                Biography = "DEMO_ Administrador principal para pruebas.",
                ProfilePicture = "https://placehold.co/256x256/2563eb/ffffff?text=AD",
                CreatedAt = now.AddDays(-10)
            };

            for (var index = 1; index <= 12; index++)
            {
                yield return new User
                {
                    Id = StableGuid($"reviewer-{index}"),
                    Username = $"DEMO_Player_{index:00}",
                    Email = $"player{index:00}{DemoDomain}",
                    PasswordHash = reviewerPasswordHash,
                    Role = Constants.Roles.Reviewer,
                    IsEmailVerified = true,
                    IsDeleted = false,
                    IsSuspended = false,
                    Biography = $"DEMO_ Jugador con gustos variados #{index}.",
                    ProfilePicture = $"https://placehold.co/256x256/0f172a/ffffff?text=P{index:00}",
                    CreatedAt = index switch
                    {
                        <= 3 => now.Date.AddHours(index),
                        <= 7 => now.AddDays(-index),
                        <= 10 => now.AddDays(-14 - index),
                        _ => now.AddMonths(-1).AddDays(index)
                    }
                };
            }
        }

        private static IEnumerable<Review> BuildReviews(IReadOnlyList<User> users, IReadOnlyList<Game> games, DateTime now)
        {
            for (var index = 0; index < 25; index++)
            {
                var user = users[index % users.Count];
                var game = games[index % games.Count];
                var createdAt = index switch
                {
                    < 8 => now.Date.AddHours(8 + index),
                    < 16 => now.AddDays(-(index % 6)).AddHours(index),
                    < 22 => now.AddDays(-12 - index),
                    _ => now.AddMonths(-1).AddDays(index)
                };
                var isVideo = index is >= 3 and <= 8;
                yield return new Review
                {
                    Id = StableGuid($"review-{index}"),
                    UserId = user.Id,
                    GameId = game.RawgId,
                    Title = $"{DemoPrefix} Review {index:00}",
                    Content = $"DEMO_ Opinion realista sobre {game.Title}: ritmo, comunidad y rendimiento probados en desarrollo.",
                    Rating = index % 7 == 0 ? 5 : index % 5 == 0 ? 6 : 8 + (index % 3),
                    LikesCount = index < 8 ? 40 - index * 3 : 8 + index,
                    DislikesCount = index % 7 == 0 ? 35 + index : index % 5,
                    ImageUrl = isVideo
                        ? $"https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4?demo={index}"
                        : $"https://placehold.co/640x360/1d4ed8/ffffff?text=DEMO+Review+{index}",
                    MediaType = isVideo ? "video" : "image",
                    CreatedAt = createdAt,
                    IsDeleted = false
                };
            }
        }

        private static IEnumerable<GameClip> BuildClips(IReadOnlyList<User> users, IReadOnlyList<Game> games, DateTime now)
        {
            for (var index = 0; index < 12; index++)
            {
                yield return new GameClip
                {
                    Id = StableGuid($"clip-{index}"),
                    UserId = users[index % users.Count].Id,
                    GameId = games[index % games.Count].Id,
                    Title = $"{DemoPrefix} Clip {index:00}",
                    Description = $"DEMO_ Clip destacado de desarrollo #{index}.",
                    Url = $"https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4?demoClip={index}",
                    CreatedAt = index < 5 ? now.AddDays(-index) : now.AddDays(-10 - index),
                    IsDeleted = false
                };
            }
        }

        private async Task<DemoSeedResultDto> SeedMongoAsync(IReadOnlyList<User> users, IReadOnlyList<Review> reviews, DateTime now, CancellationToken cancellationToken)
        {
            var socialDb = GetSocialDatabase();
            var dropsDb = GetDropsDatabase();
            var comments = BuildCommentDocuments(users, reviews, now).ToList();
            var votes = BuildVoteDocuments(users, reviews).ToList();
            var reports = BuildReportDocuments(users, reviews, comments, now).ToList();
            var events = BuildDropEventDocuments(users, now).ToList();
            var participants = BuildParticipantDocuments(users, events, now).ToList();

            if (comments.Count > 0) await socialDb.GetCollection<BsonDocument>("comments").InsertManyAsync(comments, cancellationToken: cancellationToken);
            if (votes.Count > 0) await socialDb.GetCollection<BsonDocument>("votes").InsertManyAsync(votes, cancellationToken: cancellationToken);
            if (reports.Count > 0) await socialDb.GetCollection<BsonDocument>("reports").InsertManyAsync(reports, cancellationToken: cancellationToken);
            if (events.Count > 0) await dropsDb.GetCollection<BsonDocument>("events").InsertManyAsync(events, cancellationToken: cancellationToken);
            if (participants.Count > 0) await dropsDb.GetCollection<BsonDocument>("event_participants").InsertManyAsync(participants, cancellationToken: cancellationToken);

            return new DemoSeedResultDto
            {
                Comments = comments.Count,
                Votes = votes.Count,
                Reports = reports.Count,
                DropEvents = events.Count,
                DropParticipants = participants.Count
            };
        }

        private async Task<DemoSeedResultDto> CleanupMongoAsync(IReadOnlyList<Guid> demoUserIds, CancellationToken cancellationToken)
        {
            var userIds = demoUserIds.Select(id => id.ToString()).ToList();
            var socialDb = GetSocialDatabase();
            var dropsDb = GetDropsDatabase();
            var comments = socialDb.GetCollection<BsonDocument>("comments");
            var votes = socialDb.GetCollection<BsonDocument>("votes");
            var reports = socialDb.GetCollection<BsonDocument>("reports");
            var events = dropsDb.GetCollection<BsonDocument>("events");
            var participants = dropsDb.GetCollection<BsonDocument>("event_participants");

            var eventIds = await events.Find(Builders<BsonDocument>.Filter.Regex("title", new BsonRegularExpression($"^{DemoPrefix}")))
                .Project(document => document["_id"].ToString())
                .ToListAsync(cancellationToken);

            var commentDelete = await comments.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In(UserIdField, userIds) |
                Builders<BsonDocument>.Filter.Regex("content", new BsonRegularExpression($"^{DemoPrefix}")),
                cancellationToken
            );
            var voteDelete = await votes.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In(UserIdField, userIds) |
                Builders<BsonDocument>.Filter.Regex("_id", new BsonRegularExpression($"^{DemoPrefix.ToLowerInvariant()}vote-")),
                cancellationToken
            );
            var reportDelete = await reports.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("reporterId", userIds) |
                Builders<BsonDocument>.Filter.Regex("description", new BsonRegularExpression($"^{DemoPrefix}")),
                cancellationToken
            );
            var participantDelete = await participants.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("eventId", eventIds) |
                Builders<BsonDocument>.Filter.In(UserIdField, userIds),
                cancellationToken
            );
            var eventDelete = await events.DeleteManyAsync(Builders<BsonDocument>.Filter.Regex("title", new BsonRegularExpression($"^{DemoPrefix}")), cancellationToken);

            return new DemoSeedResultDto
            {
                Comments = (int)commentDelete.DeletedCount,
                Votes = (int)voteDelete.DeletedCount,
                Reports = (int)reportDelete.DeletedCount,
                DropEvents = (int)eventDelete.DeletedCount,
                DropParticipants = (int)participantDelete.DeletedCount
            };
        }

        private static IEnumerable<BsonDocument> BuildCommentDocuments(IReadOnlyList<User> users, IReadOnlyList<Review> reviews, DateTime now)
        {
            var commentIndex = 0;
            foreach (var review in reviews.Take(16))
            {
                var count = review.CreatedAt.Date == now.Date ? 8 - (commentIndex % 4) : 2 + (commentIndex % 5);
                for (var i = 0; i < count; i++)
                {
                    yield return new BsonDocument
                    {
                        ["_id"] = $"{DemoPrefix.ToLowerInvariant()}comment-{review.Id}-{i}",
                        [UserIdField] = users[(commentIndex + i) % users.Count].Id.ToString(),
                        ["reviewId"] = review.Id.ToString(),
                        ["gameId"] = review.GameId.ToString(),
                        ["content"] = $"{DemoPrefix} Comentario {commentIndex:00}-{i:00} con una respuesta de comunidad realista.",
                        ["publishedAt"] = review.CreatedAt.AddMinutes(10 + i * 7)
                    };
                }
                commentIndex++;
            }
        }

        private static IEnumerable<BsonDocument> BuildVoteDocuments(IReadOnlyList<User> users, IReadOnlyList<Review> reviews)
        {
            foreach (var review in reviews.Take(20))
            {
                for (var i = 0; i < Math.Min(6, users.Count); i++)
                {
                    yield return new BsonDocument
                    {
                        ["_id"] = $"{DemoPrefix.ToLowerInvariant()}vote-{review.Id}-{i}",
                        [UserIdField] = users[i].Id.ToString(),
                        ["targetId"] = review.Id.ToString(),
                        ["targetType"] = "REVIEW",
                        ["isPositive"] = i % 4 != 0
                    };
                }
            }
        }

        private static IEnumerable<BsonDocument> BuildReportDocuments(IReadOnlyList<User> users, IReadOnlyList<Review> reviews, IReadOnlyList<BsonDocument> comments, DateTime now)
        {
            var reasons = new[] { "ACOSO", "SPAM", "CONTENIDO_INAPROPIADO" };
            for (var index = 0; index < 9; index++)
            {
                var resolved = index >= 6;
                var targetType = index % 3 == 0 ? "REVIEW" : index % 3 == 1 ? "COMMENT" : "USER";
                var targetId = targetType switch
                {
                    "COMMENT" => comments.ElementAtOrDefault(index)?["_id"].ToString() ?? reviews[index].Id.ToString(),
                    "USER" => users[(index + 2) % users.Count].Id.ToString(),
                    _ => reviews[index % reviews.Count].Id.ToString()
                };

                yield return new BsonDocument
                {
                    ["reporterId"] = users[index % users.Count].Id.ToString(),
                    ["targetId"] = targetId,
                    ["targetType"] = targetType,
                    ["reason"] = reasons[index % reasons.Length],
                    ["description"] = $"{DemoPrefix} Reporte de desarrollo #{index}.",
                    ["status"] = resolved ? "RESOLVED" : StatusPending,
                    ["reportedAt"] = now.AddDays(-index),
                    ["moderatorId"] = resolved ? users[0].Id.ToString() : "",
                    ["resolutionNotes"] = resolved ? $"{DemoPrefix} Accion aplicada por demo seed." : "",
                    ["resolvedAt"] = resolved ? new DateTimeOffset(now.AddDays(-index + 1)).ToUnixTimeMilliseconds() : 0L
                };
            }
        }

        private static IEnumerable<BsonDocument> BuildDropEventDocuments(IReadOnlyList<User> users, DateTime now)
        {
            var definitions = new[]
            {
                new { Key = "active-1", Status = "ACTIVE_JOIN", Offset = -1, Winner = "", Reward = StatusPending },
                new { Key = "active-2", Status = "ACTIVE_JOIN", Offset = 0, Winner = "", Reward = StatusPending },
                new { Key = "upcoming-1", Status = "UPCOMING", Offset = 2, Winner = "", Reward = StatusPending },
                new { Key = "upcoming-2", Status = "UPCOMING", Offset = 4, Winner = "", Reward = StatusPending },
                new { Key = "past-winner-pending", Status = "FINISHED", Offset = -10, Winner = users[2].Id.ToString(), Reward = StatusPending },
                new { Key = "past-winner-sent", Status = "EXHAUSTED", Offset = -14, Winner = users[3].Id.ToString(), Reward = "SENT" }
            };

            foreach (var item in definitions)
            {
                var start = now.AddDays(item.Offset);
                var winnerUser = users.FirstOrDefault(user => user.Id.ToString() == item.Winner);
                var rewardCodes = new BsonArray(Enumerable.Range(1, 3).Select(codeIndex =>
                {
                    var claimed = codeIndex == 1 && item.Winner != "";
                    return new BsonDocument
                    {
                        ["code"] = $"{DemoPrefix}REWARD-{item.Key}-{codeIndex}",
                        ["claimed"] = claimed,
                        ["claimedByUserId"] = claimed ? item.Winner : "",
                        ["claimedByUsername"] = claimed ? winnerUser?.Username ?? "" : "",
                        ["claimedAt"] = claimed ? new BsonInt64(ToUnixMilliseconds(start.AddHours(4))) : BsonNull.Value,
                        ["deliveryStatus"] = claimed ? item.Reward : StatusPending
                    };
                }));
                var winners = item.Winner == ""
                    ? new BsonArray()
                    : new BsonArray
                    {
                        new BsonDocument
                        {
                            [UserIdField] = item.Winner,
                            ["username"] = winnerUser?.Username ?? "",
                            ["rewardCode"] = $"{DemoPrefix}REWARD-{item.Key}-1",
                            ["claimedAt"] = new BsonInt64(ToUnixMilliseconds(start.AddHours(4))),
                            ["deliveryStatus"] = item.Reward
                        }
                    };
                yield return new BsonDocument
                {
                    ["_id"] = $"{DemoPrefix.ToLowerInvariant()}{item.Key}",
                    ["title"] = $"{DemoPrefix} Sorteo {item.Key}",
                    ["description"] = "DEMO_ Sorteo de llaves para visualizar el flujo completo.",
                    ["imageUrl"] = $"https://placehold.co/800x450/2563eb/ffffff?text={item.Key}",
                    ["gameTitle"] = item.Key.Contains('2') ? "Red Dead Redemption 2" : "Elden Ring",
                    ["rawgGameId"] = item.Key.Contains('2') ? 28 : 326243,
                    ["platform"] = item.Key.Contains('2') ? "Xbox" : "PC",
                    ["keysTotal"] = 3,
                    ["keysAvailable"] = item.Winner == "" ? 3 : 2,
                    ["totalSlots"] = 100,
                    ["availableSlots"] = item.Winner == "" ? 90 : 89,
                    ["status"] = item.Status,
                    ["startAt"] = ToUnixMilliseconds(start),
                    ["joinDeadlineAt"] = ToUnixMilliseconds(start.AddHours(2)),
                    ["revealAt"] = ToUnixMilliseconds(start.AddHours(3)),
                    ["endAt"] = ToUnixMilliseconds(start.AddHours(5)),
                    ["publicChallengeCode"] = "",
                    ["createdByAdminId"] = users[0].Id.ToString(),
                    ["winnerUserId"] = item.Winner,
                    ["winnerUsername"] = winnerUser?.Username ?? "",
                    ["finishedAt"] = item.Winner == "" ? BsonNull.Value : new BsonInt64(ToUnixMilliseconds(start.AddHours(4))),
                    ["rewardSentAt"] = item.Reward == "SENT" ? new BsonInt64(ToUnixMilliseconds(start.AddHours(8))) : BsonNull.Value,
                    ["rewardDeliveryStatus"] = item.Reward,
                    ["participantsCount"] = 10,
                    ["rewardCodes"] = rewardCodes,
                    ["winners"] = winners
                };
            }
        }

        private static IEnumerable<BsonDocument> BuildParticipantDocuments(IReadOnlyList<User> users, IReadOnlyList<BsonDocument> events, DateTime now)
        {
            foreach (var eventDocument in events)
            {
                foreach (var user in users.Skip(1).Take(10))
                {
                    yield return new BsonDocument
                    {
                        ["eventId"] = eventDocument["_id"].AsString,
                        [UserIdField] = user.Id.ToString(),
                        ["joinedAt"] = ToUnixMilliseconds(now.AddDays(-1))
                    };
                }
            }
        }

        private IMongoDatabase GetSocialDatabase()
        {
            return new MongoClient(_options.SocialMongoConnectionString).GetDatabase(_options.SocialDatabaseName);
        }

        private IMongoDatabase GetDropsDatabase()
        {
            return new MongoClient(_options.DropsMongoConnectionString).GetDatabase(_options.DropsDatabaseName);
        }

        private static long ToUnixMilliseconds(DateTime value)
        {
            return new DateTimeOffset(value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime()).ToUnixTimeMilliseconds();
        }

        private static Guid StableGuid(string key)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{DemoPrefix}{key}"));
            var value = Math.Abs(BitConverter.ToInt32(bytes, 0));
            return GameMappingUtilities.GenerateDeterministicGuid(value);
        }

        private static IEnumerable<Game> BuildFallbackGames()
        {
            var titles = new[] { "Elden Ring", "Red Dead Redemption 2", "Halo Infinite", "Hades", "Celeste", "Cyberpunk 2077", "Stardew Valley", "Doom Eternal", "Hollow Knight", "Baldur's Gate 3", "Forza Horizon 5", "Resident Evil 4" };
            for (var index = 0; index < titles.Length; index++)
            {
                yield return new Game
                {
                    Id = GameMappingUtilities.GenerateDeterministicGuid(900000 + index),
                    RawgId = 900000 + index,
                    Title = titles[index],
                    Developer = "DEMO_ Studio",
                    Description = $"DEMO_ Fallback catalog entry for {titles[index]}",
                    ReleaseDate = DateTime.UtcNow.AddDays(-30 - index),
                    CoverImageUrl = $"https://placehold.co/512x720/1d4ed8/ffffff?text={Uri.EscapeDataString(titles[index])}",
                    GenreIds = [index % 5 + 1],
                    PlatformIds = [index % 5 + 1]
                };
            }
        }
    }
}
