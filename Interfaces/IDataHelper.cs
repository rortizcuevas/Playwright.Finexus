using System.Data;

namespace Playwright.Finexus.Interfaces;

public interface IDataHelper
{
    DataTable ReadJsonData(string filePath, string testName);
}
