using TwitterSharp.Client;

namespace api
{
    public class BatchExecutor<T, R>
    {
        private readonly SemaphoreSlim _Semaphore;
        private Func<T, Task<R>> TaskGenerator;
        private Func<T, string> ItemDescriptionGenerator;
        private bool BackingOff = false;

        public BatchExecutor(int concurrentJobs, Func<T, Task<R>> taskGenerator, Func<T, string> itemDescriptionGenerator)
        {
            _Semaphore = new SemaphoreSlim(concurrentJobs, concurrentJobs);
            TaskGenerator = taskGenerator;
            ItemDescriptionGenerator = itemDescriptionGenerator;
        }

        public async Task<List<R>> RunFor(List<T> inputs)
        {
            var tasks = inputs
                .Select(i => RunFor(i))
                .ToList();
            
            await Task.WhenAll(tasks);

            var errors = tasks
                .Where(t => t.Exception != null)
                .Select(t => t.Exception!)
                .ToList();
            
            if(errors.Count > 0)
            {
                throw new AggregateException($"{nameof(RunFor)}(List<{typeof(T).Name}>) encountered exceptions.", errors);
            }

            var results = tasks.Select(t => t.Result).ToList();
            return results;
        }

        private async Task<R> RunFor(T input)
        {
            await _Semaphore.WaitAsync();

            try
            {
                if(BackingOff)
                {
                    throw new Exception("Backoff requested by Twitter API.");
                }
                var task = TaskGenerator(input);
                var result = await task;
                return result;
            }
            catch(BackOffException ex)
            {
                BackingOff = true;
                throw new Exception($"{nameof(RunFor)} {ItemDescriptionGenerator(input)} failed withh error {ex.GetType().Name}", ex);
            }
            catch(ArgumentNullException ex)
            {
                // This type of exception shows up for the false reports of tweets missing from the API.
                throw new Exception($"{nameof(RunFor)} {ItemDescriptionGenerator(input)} failed withh error {ex.GetType().Name}\nStatck Trace:{ex.StackTrace}\nInner Exception: {ex.InnerException?.GetType().Name} {ex.InnerException?.Message}", ex);
            }
            catch(TwitterException ex)
            {
                // This type of exception has lots of extra data we need to draw.
                throw new Exception($"{nameof(RunFor)} {ItemDescriptionGenerator(input)}.\n\tTitle:{ex.Title}\n\tType:{ex.Type}\n\tData:{ex.Data}\n\tErrors({ex.Errors?.Count() ?? 0}){string.Join("\n\t\t", ex.Errors?.Select(e => $"Title: {e.Title} Type: {e.Type} Code: {e.Code} Message:{e.Message} Details: {e.Details} Parameter: {e.Parameter} Value: {e.Value}").ToList() ?? new List<string>())}", ex);
            }
            catch(Exception ex)
            {
                throw new Exception($"{nameof(RunFor)} {ItemDescriptionGenerator(input)} failed withh error {ex.GetType().Name}", ex);
            }
            finally
            {
                _Semaphore.Release();
            }
        }
    }

    public class BackOffException : Exception
    {
        public DateTimeOffset Encountered { get; } = DateTimeOffset.Now;
    }
}
