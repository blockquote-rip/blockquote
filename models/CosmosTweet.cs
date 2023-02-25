using TwitterSharp.Response.RMedia;
using TwitterSharp.Response.RTweet;
using TwitterSharp.Response.RUser;

namespace Blockquote.Models
{
    public class CosmosTweet
    {
        public string id { get; set; }//It's obnoxious, but UpsertItemAsync<>() insists Id be lowercase.
        public DateTimeOffset CreatedAt { get; set ;} = DateTimeOffset.Now;
        public CosmosTweetUser? CreatedBy {get; set;}
        public List<CosmosTweetMedia> Media {get; set;} = new List<CosmosTweetMedia>();
        public CosmosTweet? QuotedTweet {get;set;}
        public CosmosTweet? InReplyTo {get;set;}
        public string Text {get;set;} = string.Empty;
        public string Url {get;set;} = string.Empty;
        public DateTimeOffset LastUpdated {get; set;}
        public bool Deleted {get; set;} = false;
        public DateTimeOffset NextUpdate { get => WhenToUpdate(); }


        private DateTimeOffset WhenToUpdate()
        {
            var ageInHours = (DateTimeOffset.Now - CreatedAt).TotalHours;
            if(ageInHours < 1)
                return LastUpdated.AddMinutes(10);
            else if(ageInHours < 8)
                return LastUpdated.AddMinutes(30);
            else if(ageInHours < 24)
                return LastUpdated.AddHours(1);
            else
                return LastUpdated.AddDays(7);
        }

        public CosmosTweet() {
            id = string.Empty;
        }

        public CosmosTweet(Tweet tweet)
        {
            id = tweet.Id;
            CreatedAt = new DateTimeOffset(tweet.CreatedAt ?? DateTime.Now);
            LastUpdated = CreatedAt;
            CreatedBy = new CosmosTweetUser(tweet.Author);
            Media = tweet.Attachments?.Media?.Select(m => new CosmosTweetMedia(m)).ToList() ?? new List<CosmosTweetMedia>();
            Text = tweet.Text;
            Url = $"https://twitter.com/{tweet.Author.Username}/status/{tweet.Id}";
            
            var qt = tweet.ReferencedTweets?.FirstOrDefault(r => r.Type == ReferenceType.Quoted);
            QuotedTweet = qt == null ? null : new CosmosTweet(qt);

            var irt = tweet.ReferencedTweets?.FirstOrDefault(r => r.Type == ReferenceType.RepliedTo);
            InReplyTo = irt == null ? null : new CosmosTweet(irt);
        }

        public CosmosTweet(ReferencedTweet tweet)
        {
            id = tweet.Id;
        }
    }

    public class CosmosTweetUser
    {
        public string Id { get; set; } = string.Empty;
        public string Name {get; set;} = string.Empty;
        public string ScreenName {get; set;} = string.Empty;
        public string ProfileImageUrl {get; set;} = string.Empty;
        public bool Verified {get;set;}

        public CosmosTweetUser() {}

        public CosmosTweetUser(User user)
        {
            Id = user.Id;
            Name = user.Name;
            ScreenName = user.Username;
            ProfileImageUrl = user.ProfileImageUrl;
            Verified = user.Verified ?? false;
        }
    }

    public class CosmosTweetMedia
    {
        public string? Thumbnail {get; set;}
        public string? Url {get;set;}
        public string MediaType {get;set;} = TwitterSharp.Response.RMedia.MediaType.Photo.ToString();

        public CosmosTweetMedia() {}
        public CosmosTweetMedia(Media media)
        {
            Url = media.Url;
            Thumbnail = media.PreviewImageUrl ?? media.Url;
            MediaType = media.Type?.ToString() ?? TwitterSharp.Response.RMedia.MediaType.Photo.ToString();
        }
    }
}