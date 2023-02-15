using TwitterSharp.Response.RMedia;
using TwitterSharp.Response.RTweet;
using TwitterSharp.Response.RUser;

namespace monitor
{
    public class CosmosTweet
    {
        public string id { get; set; }//It's obnoxious, but UpsertItemAsync<>() insists Id be lowercase.
        public DateTimeOffset CreatedAt { get; set ;}
        public CosmosTweetUser CreatedBy {get; set;}
        public List<CosmosTweetMedia> Media {get; set;}
        public CosmosTweet QuotedTweet {get;set;}
        public CosmosTweet InReplyTo {get;set;}
        public string Text {get;set;}
        public string Url {get;set;}
        public DateTimeOffset LastUpdated {get; set;}
        public bool Deleted {get; set;}
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

        public CosmosTweet() 
        {
            Media = new List<CosmosTweetMedia>();
            Deleted = false;
        }
        public CosmosTweet(Tweet tweet)
        {
            id = tweet.Id;
            CreatedAt = new DateTimeOffset(tweet.CreatedAt ?? DateTime.Now);
            LastUpdated = CreatedAt;
            CreatedBy = new CosmosTweetUser(tweet.Author);
            Media = tweet.Attachments.Media.Select(m => new CosmosTweetMedia(m)).ToList();
            Text = tweet.Text;
            Url = $"https://twitter.com/{tweet.Author.Username}/status/{tweet.Id}";
            
            var qt = tweet.ReferencedTweets.FirstOrDefault(r => r.Type == ReferenceType.Quoted);
            QuotedTweet = qt == null ? null : new CosmosTweet(qt);
        }

        public CosmosTweet(ReferencedTweet tweet)
        {
            id = tweet.Id;
        }
    }

    public class CosmosTweetUser
    {
        public string Id { get; set; }
        public string Name {get; set;}
        public string ScreenName {get; set;}
        public string ProfileImageUrl {get; set;}
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
        public string Thumbnail {get; set;}
        public string Url {get;set;}
        public string MediaType {get;set;}

        public CosmosTweetMedia() {}

        public CosmosTweetMedia(Media media)
        {
            Thumbnail = media.PreviewImageUrl;
            Url = media.Url;
            MediaType = media.Type?.ToString() ?? TwitterSharp.Response.RMedia.MediaType.Photo.ToString();
        }
    }
}