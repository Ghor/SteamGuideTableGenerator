using System.Text;

public class SteamMarkdownBuilder
{
    public SteamMarkdownBuilder(StringBuilder stringBuilder)
    {
        this.stringBuilder = stringBuilder;
    }

    public IDisposable NewTag(string tagName, bool ownLine = false)
    {
        return new SteamTag(this, tagName, ownLine);
    }

    private int indentLevel;
    private readonly StringBuilder stringBuilder;
    private bool lineEmpty = true;

    public SteamMarkdownBuilder Append(string text)
    {
        stringBuilder.Append(text);
        lineEmpty = false;
        return this;
    }

    public SteamMarkdownBuilder AppendLine()
    {
        stringBuilder.AppendLine().Append(' ', indentLevel * 2);
        lineEmpty = true;
        return this;
    }

    public SteamMarkdownBuilder EnsureOnEmptyLine()
    {
        if (!lineEmpty)
        {
            AppendLine();
        }
        return this;
    }

    private class SteamTag : IDisposable
    {
        private SteamMarkdownBuilder owner;
        private readonly string tagName;
        private readonly bool ownLine;

        public SteamTag(SteamMarkdownBuilder owner, string tagName, bool ownLine = false)
        {
            this.owner = owner;
            this.tagName = tagName;
            this.ownLine = ownLine;

            if (ownLine)
            {
                owner.EnsureOnEmptyLine();
                ++owner.indentLevel;
            }

            owner.Append("[").Append(tagName).Append("]");
            if (ownLine)
            {
                owner.EnsureOnEmptyLine();
            }
        }

        public void Dispose()
        {
            if (owner == null)
            {
                return;
            }

            if (ownLine)
            {
                --owner.indentLevel;
                owner.EnsureOnEmptyLine();
            }
            owner.Append("[/").Append(tagName).Append("]");
        }
    }
}