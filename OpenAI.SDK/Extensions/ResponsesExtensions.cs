using Betalgo.Ranul.OpenAI.Contracts.Responses.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;

namespace Betalgo.Ranul.OpenAI;

/// <summary>
///     Extension methods for <see cref="Response" />.
/// </summary>
public static class ResponsesExtensions
{
    /// <summary>
    ///     Gets all text content from the output items.
    /// </summary>
    /// <param name="response">The response to extract text from.</param>
    /// <returns>The concatenated text from all output message items.</returns>
    public static string? GetOutputText(this Response response)
    {
        if (!string.IsNullOrEmpty(response.OutputText))
            return response.OutputText;

        if (response.Output == null || response.Output.Count == 0)
            return null;

        var texts = new List<string>();
        foreach (var item in response.Output)
        {
            if (item is OutputMessageItem message)
            {
                foreach (var content in message.Content)
                {
                    if (content is OutputTextContent textContent)
                    {
                        texts.Add(textContent.Text);
                    }
                }
            }
        }

        return texts.Count > 0 ? string.Join("", texts) : null;
    }
}

