﻿

namespace Basiscore.Minions.Utilities
{
    using Basiscore.Minions.Models;
    using ClosedXML.Excel;
    using Sitecore;
    using Sitecore.Collections;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using Sitecore.Globalization;
    using Sitecore.Layouts;
    using Sitecore.Publishing;
    using Sitecore.Publishing.Pipelines.Publish;
    using Sitecore.SecurityModel;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Web;

    public class MinionHelper
    {
        public enum RenderingInstancePosition
        {
            First = 1,
            Last = 2,
            SpecifiedIndex = 3
        }

        public struct Databases
        {
            public static Database masterDb = Factory.GetDatabase(MinionConstants.DatabaseNames.Master);
        }

        public static Language DefaultLanguage
        {
            get
            {
                return LanguageManager.GetLanguages(Databases.masterDb)
                                .Where(x => x.Name.ToLower() == "en").FirstOrDefault();
            }
        }

        public static bool IsUserLoggedIn()
        {
            return Sitecore.Context.IsLoggedIn;
        }

        public static bool IsValidID(ID id)
        {
            return (!string.IsNullOrEmpty(System.Convert.ToString(id)) && !id.IsNull && !id.IsGlobalNullId);
        }

        public static bool IsValidID(string id)
        {
            bool isValidId = false;

            ID itemId = null;

            try
            {
                itemId = new ID(id);
                isValidId = !itemId.IsNull && !itemId.IsGlobalNullId;
            }
            catch (Exception)
            {

            }

            return isValidId;
        }

        /// <summary>
        /// checks if an item has a field
        /// </summary>
        /// <param name="contextItem"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static bool ItemHasField(Item contextItem, ID fieldID)
        {
            //contextItem.Fields.ReadAll();
            return contextItem.Template.Fields.Any(x => x.ID == fieldID);
        }

        public static bool ItemHasField(Item contextItem, string fieldName)
        {
            //contextItem.Fields.ReadAll();
            return contextItem.Template.Fields.Any(x => x.Name == fieldName);
        }

        public static Database GetDatabase(string databaseName)
        {
            return Factory.GetDatabase(databaseName);
        }

        public static Item GetItem(ID itemID, Language language = null, string databaseName = "")
        {
            Item item = null;
            Database db = string.IsNullOrEmpty(databaseName) ? GetDatabase(MinionConstants.DatabaseNames.Master) : GetDatabase(databaseName);

            if (db != null)
            {
                if (language == null)
                {
                    language = DefaultLanguage;
                }

                if (language != null)
                {
                    using (new SecurityDisabler())
                    {
                        using (new LanguageSwitcher(language.Name))
                        {
                            item = db.GetItem(itemID, language);
                        }
                    }
                }
            }

            return item;
        }

        public static Item GetItem(string itemPath, Language language = null, string databaseName = "")
        {
            Item item = null;
            Database db = string.IsNullOrEmpty(databaseName) ? GetDatabase(MinionConstants.DatabaseNames.Master) : GetDatabase(databaseName);

            if (db != null)
            {
                if (language == null)
                {
                    language = DefaultLanguage;
                }

                if (language != null)
                {
                    using (new SecurityDisabler())
                    {
                        using (new LanguageSwitcher(language.Name))
                        {
                            item = db.GetItem(itemPath, language);
                        }
                    }
                }
            }

            return item;
        }

        public static Item GetItem(string itemPathOrId, string database = "")
        {
            Item item = null;

            if (!string.IsNullOrWhiteSpace(itemPathOrId))
            {
                Database db = string.IsNullOrWhiteSpace(database) ? Sitecore.Context.Database : Factory.GetDatabase(database);

                if (db != null)
                {
                    using (new SecurityDisabler())
                    {
                        item = db.GetItem(itemPathOrId);
                    }
                }
            }

            return item;
        }

        public static Item GetItem(string itemPathOrId)
        {
            return GetItem(itemPathOrId, "");
        }

        public static List<string> GetTargetDatabases()
        {
            List<string> lstTargetDatabases = null;
            Item publishingTargetsItem = GetItem(MinionConstants.Items.PublishingTargets);

            if (publishingTargetsItem != null)
            {
                List<Item> lstPublishingTargets = GetItemsByTemplate(publishingTargetsItem, MinionConstants.Templates.PublishingTarget.ID);

                if (lstPublishingTargets != null && lstPublishingTargets.Count > 0)
                {
                    lstTargetDatabases = new List<string>();
                    IEnumerable<string> allTargetDatabases = lstPublishingTargets
                        .Where(x => !string.IsNullOrEmpty(System.Convert.ToString(x.Fields[MinionConstants.Templates.PublishingTarget.Fields.TargetDatabase].Value).Trim()))
                    .Select(x => System.Convert.ToString(x.Fields[MinionConstants.Templates.PublishingTarget.Fields.TargetDatabase].Value).Trim());

                    if (allTargetDatabases != null && allTargetDatabases.Count() > 0)
                    {
                        lstTargetDatabases.AddRange(allTargetDatabases);
                    }
                }
            }

            return lstTargetDatabases;
        }

        public static List<KeyValuePair<string, string>> GetPublishingTargets()
        {
            List<KeyValuePair<string, string>> lstTargetDatabases = null;
            Item publishingTargetsItem = GetItem(MinionConstants.Items.PublishingTargets);

            if (publishingTargetsItem != null)
            {
                List<Item> lstPublishingTargets = GetItemsByTemplate(publishingTargetsItem, MinionConstants.Templates.PublishingTarget.ID);

                if (lstPublishingTargets != null && lstPublishingTargets.Count > 0)
                {
                    lstTargetDatabases = new List<KeyValuePair<string, string>>();
                    string dbName = "";

                    foreach (Item item in lstPublishingTargets)
                    {
                        dbName = System.Convert.ToString(item.Fields[MinionConstants.Templates.PublishingTarget.Fields.TargetDatabase].Value).Trim();

                        if (!string.IsNullOrEmpty(dbName))
                        {
                            lstTargetDatabases.Add(new KeyValuePair<string, string>(dbName + " (" + item.Name + ")", dbName));
                        }
                    }
                }
            }

            return lstTargetDatabases;
        }

        /// <summary>
        /// get all the user selected target languages
        /// </summary>
        /// <param name="commaSeparatedLanguageCodes"></param>
        /// <returns></returns>
        public static List<Language> GetTargetLanguages(string commaSeparatedLanguageCodes)
        {
            List<Language> lstLanguages = new List<Language>();

            if (!string.IsNullOrEmpty(commaSeparatedLanguageCodes))
            {
                string[] targetLanguageCodes = commaSeparatedLanguageCodes.Split(',');

                if (targetLanguageCodes != null && targetLanguageCodes.Length > 0)
                {
                    Language language = null;

                    foreach (string languageCode in targetLanguageCodes)
                    {
                        if (!string.IsNullOrEmpty(languageCode))
                        {
                            language = LanguageManager.GetLanguages(Databases.masterDb)
                                .Where(x => x.Name.ToLower() == languageCode.ToLower()).FirstOrDefault();

                            if (language != null)
                            {
                                lstLanguages.Add(language);
                            }
                        }
                    }
                }
            }

            return lstLanguages;
        }

        /// <summary>
        /// get the list of installed languages
        /// </summary>
        /// <returns></returns>
        public static List<KeyValuePair<string, string>> GetInstalledLanguages()
        {
            //Dictionary<string, string> installedLanguages = new Dictionary<string, string>();
            List<KeyValuePair<string, string>> lstInstalledLanguages = new List<KeyValuePair<string, string>>();
            LanguageCollection languageCollection = LanguageManager.GetLanguages(Databases.masterDb);

            if (languageCollection != null && languageCollection.Count > 0)
            {
                foreach (Language language in languageCollection)
                {
                    lstInstalledLanguages.Add(new KeyValuePair<string, string>(language.Name, language.CultureInfo.DisplayName));
                }
                //lstInstalledLanguages = languageCollection.ToList();
            }

            return lstInstalledLanguages;
        }

        public static PublishResult PublishItem(Item sourceItem, string targetDatabaseName, bool publishSubItems)
        {
            PublishResult publishResult = null;

            if (sourceItem != null)
            {
                Database targetDatabase = GetDatabase(targetDatabaseName);

                if (targetDatabase != null)
                {
                    /// Create a publisher with the publishoptions
                    /// The publishOptions determine the source and target database,
                    /// the publish mode and language, and the publish date
                    PublishOptions publishOptions = new PublishOptions(Databases.masterDb, targetDatabase,
                                                         PublishMode.SingleItem, sourceItem.Language,
                                                         System.DateTime.Now);

                    if (publishOptions != null)
                    {
                        publishOptions.UserName = Sitecore.Context.User.Name;
                        MyPublisher publisher = new MyPublisher(publishOptions);

                        if (publisher != null)
                        {
                            /// Choose where to publish from
                            publisher.Options.RootItem = sourceItem;

                            /// Publish children as well?
                            publisher.Options.Deep = publishSubItems;

                            /// Do the publish!
                            using (new SecurityDisabler())
                            {
                                //using (new EventDisabler())
                                //{
                                publishResult = publisher.PublishWithResult();
                                //}
                            }

                            sourceItem.Publishing.ClearPublishingCache();
                        }
                    }
                }
            }

            return publishResult;
        }

        public static void UpdateFieldValues(Item contextItem, List<KeyValuePair<ID, string>> listOfItemFieldsWithValues, bool createVersion = true)
        {
            Item itemToUpdate = null;

            if (contextItem != null && listOfItemFieldsWithValues != null && listOfItemFieldsWithValues.Count > 0)
            {
                itemToUpdate = createVersion ? contextItem.Versions.AddVersion() : contextItem;

                using (new SecurityDisabler())
                {
                    itemToUpdate.Editing.BeginEdit();

                    foreach (KeyValuePair<ID, string> kvp in listOfItemFieldsWithValues)
                    {
                        if (ItemHasField(itemToUpdate, kvp.Key))
                        {
                            itemToUpdate[kvp.Key] = kvp.Value;
                        }
                    }

                    itemToUpdate.Editing.EndEdit();
                }
            }
        }

        /// <summary>
        /// for updating by field name
        /// </summary>
        /// <param name="contextItem"></param>
        /// <param name="listOfItemFieldsWithValues"></param>
        /// <param name="createVersion"></param>
        public static void UpdateFieldValues(Item contextItem, List<KeyValuePair<string, string>> listOfItemFieldsWithValues, bool createVersion = true, string language = "en")
        {
            Item itemToUpdate = null;
            Item itemByLanguage = null;

            if (contextItem != null)
            {
                Language selectedLanguage = LanguageManager.GetLanguages(Databases.masterDb)
                                .Where(x => x.Name.ToLower() == language.ToLower()).FirstOrDefault();

                if (selectedLanguage != null)
                {
                    itemByLanguage = GetItem(contextItem.ID, selectedLanguage);

                    if (itemByLanguage != null && listOfItemFieldsWithValues != null && listOfItemFieldsWithValues.Count > 0)
                    {
                        itemToUpdate = createVersion ? itemByLanguage.Versions.AddVersion() : itemByLanguage;

                        using (new SecurityDisabler())
                        {
                            itemToUpdate.Editing.BeginEdit();

                            foreach (KeyValuePair<string, string> kvp in listOfItemFieldsWithValues)
                            {
                                if (ItemHasField(itemToUpdate, kvp.Key))
                                {
                                    itemToUpdate[kvp.Key] = kvp.Value;
                                }
                            }

                            itemToUpdate.Editing.EndEdit();
                        }
                    }
                }
            }
        }

        public static Item RemoveWorkflow(Item contextItem, Language language)
        {
            Item processedItem = null;

            if (contextItem != null)
            {
                ID itemId = contextItem.ID;
                string dbName = contextItem.Database.Name;

                using (new SecurityDisabler())
                {
                    contextItem.Editing.BeginEdit();
                    contextItem[MinionConstants.Templates.Workflow.Fields.Workflow] = string.Empty;
                    contextItem.Editing.EndEdit();
                }

                processedItem = GetItem(contextItem.ID, language, dbName);
            }

            return processedItem;
        }

        public static bool HasWorkflow(Item contextItem)
        {
            bool hasWorkflow = false;
            hasWorkflow = contextItem != null &&
                contextItem[MinionConstants.Templates.Workflow.Fields.Workflow] != null &&
                !string.IsNullOrEmpty(contextItem[MinionConstants.Templates.Workflow.Fields.Workflow]);
            return hasWorkflow;
        }

        public static List<Item> GetItemsByTemplate(Item parentItem, ID templateID, bool checkBaseTemplates = false)
        {
            List<Item> itemsByTemplate = new List<Item>();

            if (parentItem != null && IsValidID(templateID))
            {
                List<ID> usages = new List<ID>();
                TemplateItem selectedTemplateItem = Databases.masterDb.GetItem(templateID);

                if (checkBaseTemplates)
                {
                    itemsByTemplate = parentItem.Axes.GetDescendants().Where(x => x.TemplateID == templateID ||
                    (x.Template != null && x.Template.BaseTemplates.Any(b => b.ID == templateID))).ToList();
                }
                else
                {
                    itemsByTemplate = parentItem.Axes.GetDescendants().Where(x => x.TemplateID == templateID).ToList();
                }
            }

            return itemsByTemplate;

        }

        public static bool IsValidName(string name)
        {
            bool isValidName = true;
            name = name.ToLower();

            if (!string.IsNullOrEmpty(name))
            {
                char[] charsInName = name.ToCharArray();

                foreach (char character in charsInName)
                {
                    if (!MinionConstants.ValidCharacters.Contains(character))
                    {
                        isValidName = false;
                        break;
                    }
                }
            }
            else
            {
                isValidName = false;
            }

            return isValidName;
        }

        public static string ReplaceFirstInstance(string source, string keyword, string replaceWith)
        {
            int keywordPosition = source.IndexOf(keyword, 0, StringComparison.CurrentCultureIgnoreCase);

            if (keywordPosition < 0)
            {
                return source;
            }

            string result = source.Substring(0, keywordPosition) + replaceWith + source.Substring(keywordPosition + keyword.Length);
            return result;
        }

        public static string ReplaceLastInstance(string source, string keyword, string replaceWith)
        {
            int keywordPosition = source.LastIndexOf(keyword, source.Length, StringComparison.CurrentCultureIgnoreCase);

            if (keywordPosition == -1)
                return source;

            string result = source.Remove(keywordPosition, keyword.Length).Insert(keywordPosition, replaceWith);
            return result;
        }

        /// <summary>
        /// gets all the non system fields of an item including inherited.
        /// </summary>
        /// <param name="contextItem"></param>
        /// <returns></returns>
        public static List<TemplateFieldItem> GetTemplateFields(Item contextItem, bool includeSystemTemplateFields = false)
        {
            List<TemplateFieldItem> lstNonSystemTemplateFields = null;

            if (contextItem != null)
            {
                TemplateFieldItem[] templateFieldsCollection = contextItem.Template.Fields;

                if (templateFieldsCollection != null && templateFieldsCollection.Length > 0)
                {
                    if (includeSystemTemplateFields)
                    {
                        lstNonSystemTemplateFields = templateFieldsCollection.ToList();
                    }
                    else
                    {
                        lstNonSystemTemplateFields = templateFieldsCollection.Where(x => !x.InnerItem.Paths.FullPath.StartsWith(MinionConstants.Paths.SystemTemplates, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }
            }

            return lstNonSystemTemplateFields;
        }

        /// <summary>
        /// gets the template item from master database
        /// </summary>
        /// <param name="templatePath"></param>
        /// <returns></returns>
        public static TemplateItem GetTemplateItem(string templatePath)
        {
            if (!string.IsNullOrEmpty(templatePath))
            {
                //templatePath = templatePath.Trim().TrimStart('/');
                templatePath = !string.IsNullOrEmpty(templatePath) && templatePath.ToLower().StartsWith(MinionConstants.Paths.Templates) ? templatePath.ToLower().Replace(MinionConstants.Paths.Templates, "") : templatePath;
                templatePath = !string.IsNullOrEmpty(templatePath) ? templatePath.Trim().TrimEnd('/') : templatePath;
                return !string.IsNullOrEmpty(templatePath) ? Databases.masterDb.GetTemplate(templatePath) : null;
            }

            return null;
        }

        public static DataTable GetExcelData(Stream inputStream)
        {
            DataTable dtData = null;

            ///Open the Excel file using ClosedXML.
            using (XLWorkbook workBook = new XLWorkbook(inputStream))
            {
                ///Read the first Sheet from Excel file.
                IXLWorksheet workSheet = workBook.Worksheet(1);

                ///Create a new DataTable.
                dtData = new DataTable();

                ///Loop through the Worksheet rows.
                bool firstRow = true;

                foreach (IXLRow row in workSheet.Rows())
                {
                    ///Use the first row to add columns to DataTable.
                    if (firstRow)
                    {
                        foreach (IXLCell cell in row.Cells())
                        {
                            dtData.Columns.Add(cell.Value.ToString());
                        }

                        firstRow = false;
                    }
                    else
                    {
                        ///Add rows to DataTable.
                        dtData.Rows.Add();
                        int i = 0;

                        foreach (IXLCell cell in row.Cells(1, dtData.Columns.Count))
                        {
                            dtData.Rows[dtData.Rows.Count - 1][i] = cell.Value.ToString();
                            i++;
                        }
                    }
                }
            }

            return dtData;
        }

        public static Item CreateItem(string itemName, Item parentItem, TemplateItem templateItem,
            List<KeyValuePair<string, string>> listOfItemFieldsWithValues, string language = "en",
            BranchItem branchItem = null)
        {
            Item newItem = null;
            string parentItemId = parentItem.ID.ToString();

            using (new SecurityDisabler())
            {
                Language selectedLanguage = LanguageManager.GetLanguages(Databases.masterDb)
                                .Where(x => x.Name.ToLower() == language.ToLower()).FirstOrDefault();

                if (selectedLanguage != null)
                {
                    parentItem = GetItem(parentItemId, selectedLanguage);

                    if (templateItem != null)
                    {
                        newItem = parentItem.Add(itemName, templateItem);
                    }
                    else if (branchItem != null)
                    {
                        newItem = parentItem.Add(itemName, branchItem);
                    }

                    if (newItem != null)
                    {
                        newItem.Editing.BeginEdit();

                        if (listOfItemFieldsWithValues != null && listOfItemFieldsWithValues.Count > 0)
                        {
                            foreach (KeyValuePair<string, string> kvp in listOfItemFieldsWithValues)
                            {
                                if (ItemHasField(newItem, kvp.Key))
                                {
                                    newItem[kvp.Key] = kvp.Value;
                                }
                            }
                        }

                        newItem.Editing.EndEdit();
                    }
                }
            }

            return newItem;
        }

        /// <summary>
        /// checks if the give input is a valid item name
        /// A valid item name should only have alphanumerics, space, hyphen, underscore
        /// </summary>
        /// <param name="itemName"></param>
        /// <returns></returns>
        public static bool IsValidItemName(string itemName)
        {
            if (!string.IsNullOrEmpty(itemName))
            {
                Regex rg = new Regex(@"^[a-zA-Z0-9-_\s]*$");
                return rg.IsMatch(itemName);
            }

            return false;
        }

        /// <summary>
        /// check if the uploaded file is a valid excel file
        /// </summary>
        /// <param name="httpPostedFile"></param>
        /// <returns></returns>
        public static bool IsValidFile(string fileName, string[] validFileExtensions)
        {
            bool isValidFile = false;

            if (fileName.Length > 0)
            {
                foreach (string extension in validFileExtensions)
                {
                    if (fileName.EndsWith(extension))
                    {
                        isValidFile = true;
                        break;
                    }
                }
            }

            return isValidFile;
        }

        public static List<Item> GetItemsFromPathOrId(List<string> lstItemPaths)
        {
            List<Item> lstItems = null;
            Item item = null;

            if (lstItemPaths != null && lstItemPaths.Count > 0)
            {
                lstItems = new List<Item>();

                foreach (string itemPath in lstItemPaths)
                {
                    item = GetItem(itemPath);

                    if (item != null)
                    {
                        lstItems.Add(item);
                    }
                }
            }

            return lstItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetItem"></param>
        /// <param name="targetLayoutId"></param>
        /// <param name="defaultDeviceId"></param>
        /// <param name="layoutField"></param>
        /// <param name="layoutDefinition"></param>
        /// <param name="deviceDefinition"></param>
        public static void GetDeviceDefinitions(Item targetItem, bool isFinalLayout, string defaultDeviceId, out LayoutField layoutField, out LayoutDefinition layoutDefinition, out DeviceDefinition deviceDefinition)
        {
            layoutField = null;
            layoutDefinition = null;
            deviceDefinition = null;
            DeviceDefinition ddef = null;

            /// Get the layout definitions and the device definition	
            if (isFinalLayout)
            {
                layoutField = new LayoutField(targetItem.Fields[FieldIDs.FinalLayoutField]);
            }
            else
            {
                layoutField = new LayoutField(targetItem.Fields[FieldIDs.LayoutField]);
            }

            layoutDefinition = layoutField != null ? LayoutDefinition.Parse(layoutField.Value) : null;
            ddef = layoutDefinition != null ? layoutDefinition.GetDevice(defaultDeviceId) : null;
            deviceDefinition = ddef != null ? layoutDefinition.GetDevice(ddef.ID.ToString()) : null;
        }

        public static bool PageHasRendering(string renderingId, IEnumerable<RenderingDefinition> renderingsArray, bool checkRenderingAtSpecifiedIndex, int renderingIndex)
        {
            bool isRenderingAvailable = renderingsArray.Any(x => x.ItemID == renderingId);

            if (checkRenderingAtSpecifiedIndex)
            {
                if (isRenderingAvailable)
                {
                    if (renderingIndex >= 0 && renderingIndex < renderingsArray.Count())
                    {
                        RenderingDefinition instanceOfRendering = renderingsArray.ElementAt(renderingIndex);

                        if (instanceOfRendering == null || instanceOfRendering.ItemID != renderingId)
                        {
                            isRenderingAvailable = false;
                        }
                    }
                }
            }

            return isRenderingAvailable;
        }

        /// <summary>	        
        /// Get the rendering definition of the target rendering from the existing page renderings	        
        /// <param name="taskId"></param>	        
        /// <param name="renderingId"></param>	        
        /// <param name="renderingsArray"></param>	        
        /// <param name="targetIndex"></param>	        
        /// <returns></returns>	        
        /// </summary>
        public static RenderingDefinition GetRenderingDefinition(string renderingId, IEnumerable<RenderingDefinition> renderingsArray, RenderingInstancePosition renderingInstancePosition, int renderingIndex)
        {
            RenderingDefinition instanceOfRendering = null;
            bool isRenderingAvailable = renderingsArray.Any(x => x.ItemID == renderingId);

            if (isRenderingAvailable)
            {
                switch (renderingInstancePosition)
                {
                    case RenderingInstancePosition.First: ///get first instance of rendering	                    
                        instanceOfRendering = renderingsArray.Where(x => x.ItemID == renderingId).First();
                        break;
                    case RenderingInstancePosition.Last: ///get last instance of rendering	                    
                        instanceOfRendering = renderingsArray.Where(x => x.ItemID == renderingId).Last();
                        break;
                    case RenderingInstancePosition.SpecifiedIndex: ///get rendering definition of rendering at specified index	  
                        if (renderingIndex >= 0 && renderingIndex < renderingsArray.Count())
                        {
                            instanceOfRendering = renderingsArray.ElementAt(renderingIndex);

                            if (instanceOfRendering == null || instanceOfRendering.ItemID != renderingId)
                            {
                                instanceOfRendering = null;
                            }
                        }
                        break;
                }
            }

            return instanceOfRendering;
        }

        /// <summary>
        /// generate a random string
        /// </summary>
        /// <param name="passwordLength"></param>
        /// <returns></returns>
        public static string GetRandomString(int stringLength)
        {
            ///This one tells you how many characters the string will contain.

            ///This one, is empty for now - but will ultimately hold the finised randomly generated password
            string newRandomString = "";

            ///This one tells you which characters are allowed in this new password
            string allowedChars = "";
            allowedChars = "1,2,3,4,5,6,7,8,9,0";
            allowedChars += "a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z";

            ///Then working with an array...

            char[] sep = { ',' };
            string[] arr = allowedChars.Split(sep);

            ///string IDString = "";
            string temp = "";

            ///utilize the "random" class
            Random rand = new Random();

            ///and lastly - loop through the generation process...
            for (int i = 0; i < System.Convert.ToInt32(stringLength); i++)
            {
                temp = arr[rand.Next(0, arr.Length)];
                newRandomString += temp;
            }

            return newRandomString;
        }

        public static void DownloadData(DataTable dt, string fileName)
        {
            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.Worksheets.Add(dt, fileName);
                HttpContext.Current.Response.Clear();
                HttpContext.Current.Response.Buffer = true;
                HttpContext.Current.Response.Charset = "";
                HttpContext.Current.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                HttpContext.Current.Response.AddHeader("content-disposition", "attachment;filename=" + fileName + ".xlsx");

                using (MemoryStream MyMemoryStream = new MemoryStream())
                {
                    wb.SaveAs(MyMemoryStream);
                    MyMemoryStream.WriteTo(HttpContext.Current.Response.OutputStream);
                    HttpContext.Current.Response.Flush();
                    HttpContext.Current.Response.SuppressContent = false; 
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                }
            }
        }

        public static List<T> GetClassObjectsFromItems<T>(List<Item> lstItems) where T : class
        {
            List<T> activeItems = new List<T>();

            if (lstItems != null && lstItems.Count > 0)
            {
                foreach (Item item in lstItems)
                {
                    T instance = (T)Activator.CreateInstance(typeof(T), args: item);

                    if (instance != null)
                    {
                        activeItems.Add(instance);
                    }
                }
            }

            return activeItems;
        }
    }
}