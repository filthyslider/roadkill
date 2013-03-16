﻿using System;
using System.Collections.Generic;
using System.Linq;
using Roadkill.Core.Configuration;
using StructureMap;
using Mindscape.LightSpeed;
using AutoMapper;
using LSSitePreferencesEntity = Roadkill.Core.Database.LightSpeed.SitePreferencesEntity;
using Mindscape.LightSpeed.Querying;
using System.Data;
using Mindscape.LightSpeed.Logging;
using Mindscape.LightSpeed.Caching;
using Mindscape.LightSpeed.Linq;
using Roadkill.Core.Database.Schema;
using Roadkill.Core.Common;

namespace Roadkill.Core.Database.LightSpeed
{
	public class LightSpeedRepository : Roadkill.Core.Database.IRepository
	{
		private IConfigurationContainer _configuration;

		internal IQueryable<PageEntity> Pages
		{
			get
			{
				return UnitOfWork.Query<PageEntity>();
			}
		}

		internal IQueryable<PageContentEntity> PageContents
		{
			get
			{
				return UnitOfWork.Query<PageContentEntity>();
			}
		}

		internal IQueryable<UserEntity> Users
		{
			get
			{
				return UnitOfWork.Query<UserEntity>();
			}
		}

		public virtual LightSpeedContext Context
		{
			get
			{
				LightSpeedContext context = ObjectFactory.GetInstance<LightSpeedContext>();
				if (context == null)
					throw new DatabaseException("The context for Lightspeed is null - has Startup() been called?", null);

				return context;
			}
		}


		public virtual IUnitOfWork UnitOfWork
		{
			get
			{
				IUnitOfWork unitOfWork = ObjectFactory.GetInstance<IUnitOfWork>();
				if (unitOfWork == null)
					throw new DatabaseException("The IUnitOfWork for Lightspeed is null - has Startup() been called?", null);

				return unitOfWork;
			}
		}

		static LightSpeedRepository()
		{
			Mapper.CreateMap<PageEntity, Page>().ReverseMap();
			Mapper.CreateMap<PageContentEntity, PageContent>().ReverseMap();
			Mapper.CreateMap<UserEntity, User>().ReverseMap();
			Mapper.CreateMap<SitePreferencesEntity, LSSitePreferencesEntity>().ReverseMap();
		}

		public LightSpeedRepository(IConfigurationContainer configuration)
		{
			_configuration = configuration;
		}

		public void DeletePage(Page page)
		{
			PageEntity entity = UnitOfWork.FindById<PageEntity>(page.Id);
			UnitOfWork.Remove(entity);
		}

		public void DeletePageContent(PageContent pageContent)
		{
			PageContentEntity entity = UnitOfWork.FindById<PageContentEntity>(pageContent.Id);
			UnitOfWork.Remove(entity);
		}

		public void DeleteUser(User user)
		{
			UserEntity entity = UnitOfWork.FindById<UserEntity>(user.Id);
			UnitOfWork.Remove(entity);
		}

		public void DeleteAllPages()
		{
			UnitOfWork.Remove(new Query(typeof(PageEntity)));
		}

		public void DeleteAllPageContent()
		{
			UnitOfWork.Remove(new Query(typeof(PageContentEntity)));
		}

		public void DeleteAllUsers()
		{
			UnitOfWork.Remove(new Query(typeof(UserEntity)));
		}

		public PageContent GetLatestPageContent(int pageId)
		{
			var source = PageContents.Where(x => x.Page.Id == pageId).OrderByDescending(x => x.EditedOn).FirstOrDefault();
			return Mapper.Map<PageContent>(source);
		}

		public SitePreferences GetSitePreferences()
		{
			SitePreferencesEntity entity = UnitOfWork.Find<SitePreferencesEntity>().FirstOrDefault();
			SitePreferences preferences = new SitePreferences();

			if (entity != null)
			{
				preferences = SitePreferences.LoadFromJson(entity.Content);
			}
			else
			{
				Log.Warn("No configuration settings could be found in the database, using a default instance");
			}

			return preferences;
		}

		public void SaveSitePreferences(SitePreferences preferences)
		{
			SitePreferencesEntity entity = UnitOfWork.Find<SitePreferencesEntity>().FirstOrDefault();

			if (entity == null || entity.Id == Guid.Empty)
			{
				entity = new SitePreferencesEntity();
				entity.Version = ApplicationSettings.AssemblyVersion.ToString();
				entity.Content = preferences.GetJson();
				UnitOfWork.Add(entity);
			}
			else
			{
				entity.Version = ApplicationSettings.AssemblyVersion.ToString();
				entity.Content = preferences.GetJson();
			}

			UnitOfWork.SaveChanges();
		}

		public void Startup(DataStoreType dataStoreType, string connectionString, bool enableCache)
		{
			if (!string.IsNullOrEmpty(connectionString))
			{
				LightSpeedContext context = new LightSpeedContext();
				context.ConnectionString = connectionString;
				context.DataProvider = dataStoreType.LightSpeedDbType;
				context.IdentityMethod = IdentityMethod.GuidComb;
				context.CascadeDeletes = false;
				context.VerboseLogging = true;

#if DEBUG
				//context.Logger = new TraceLogger();
				context.Cache = new Mindscape.LightSpeed.Caching.CacheBroker(new DefaultCache());
#endif

				ObjectFactory.Configure(x =>
				{
					x.For<LightSpeedContext>().Singleton().Use(context);
					x.For<IUnitOfWork>().HybridHttpOrThreadLocalScoped().Use(ctx => ctx.GetInstance<LightSpeedContext>().CreateUnitOfWork());
				});
			}
		}

		public void Install(DataStoreType dataStoreType, string connectionString, bool enableCache)
		{
			LightSpeedContext context = ObjectFactory.GetInstance<LightSpeedContext>();
			if (context == null)
				throw new InvalidOperationException("Repository.Install failed - LightSpeedContext was null from the ObjectFactory");

			using (IDbConnection connection = context.DataProviderObjectFactory.CreateConnection())
			{
				connection.ConnectionString = connectionString;
				connection.Open();

				IDbCommand command = context.DataProviderObjectFactory.CreateCommand();
				command.Connection = connection;

				dataStoreType.Schema.Drop(command);
				dataStoreType.Schema.Create(command);
			}
		}

		public void Test(DataStoreType dataStoreType, string connectionString)
		{
			LightSpeedContext context = ObjectFactory.GetInstance<LightSpeedContext>();
			if (context == null)
				throw new InvalidOperationException("Repository.Test failed - LightSpeedContext was null from the ObjectFactory");

			using (IDbConnection connection = context.DataProviderObjectFactory.CreateConnection())
			{
				connection.ConnectionString = connectionString;
				connection.Open();
			}
		}

		public void Upgrade(IConfigurationContainer configuration)
		{
			try
			{
				using (IDbConnection connection = Context.DataProviderObjectFactory.CreateConnection())
				{
					connection.ConnectionString = configuration.ApplicationSettings.ConnectionString;
					connection.Open();

					IDbCommand command = Context.DataProviderObjectFactory.CreateCommand();
					command.Connection = connection;

					configuration.ApplicationSettings.DataStoreType.Schema.Upgrade(command);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Upgrade failed: {0}", ex);
				throw new UpgradeException("A problem occurred upgrading the database schema.\n\n", ex);
			}

			try
			{
				SaveSitePreferences(new SitePreferences());
			}
			catch (Exception ex)
			{
				Log.Error("Upgrade failed: {0}", ex);
				throw new UpgradeException("A problem occurred saving the site preferences.\n\n", ex);
			}
		}

		public IEnumerable<Page> AllPages()
		{
			var source = Pages;
			return Mapper.Map<IEnumerable<Page>>(source);
		}

		public Page GetPageById(int id)
		{
			var source = Pages.FirstOrDefault(p => p.Id == id);
			return Mapper.Map<Page>(source);
		}

		public IEnumerable<Page> FindPagesByCreatedBy(string username)
		{
			var source = Pages.Where(p => p.CreatedBy == username);
			return Mapper.Map<IEnumerable<Page>>(source);
		}

		public IEnumerable<Page> FindPagesByModifiedBy(string username)
		{
			var source = Pages.Where(p => p.ModifiedBy == username);
			return Mapper.Map<IEnumerable<Page>>(source);
		}

		public IEnumerable<Page> FindPagesContainingTag(string tag)
		{
			IEnumerable<PageEntity> source = new List<PageEntity>();

			if (_configuration.ApplicationSettings.DataStoreType != DataStoreType.Postgres)
			{
				source = Pages.Where(p => p.Tags.ToLower().Contains(tag.ToLower()));
			}
			else
			{
				// Temporary Lightspeed Postgres LIKE bug work around
				IDbCommand command = UnitOfWork.Context.DataProviderObjectFactory.CreateCommand();
				command.CommandText = "SELECT * FROM roadkill_pages WHERE tags LIKE @Tag"; // case sensitive column name
				IDbDataParameter parameter = command.CreateParameter();
				parameter.DbType = DbType.String;
				parameter.ParameterName = "@Tag";
				parameter.Value = "%" +tag+ "%";
				command.Parameters.Add(parameter);

				source = UnitOfWork.FindBySql<PageEntity>(command);
			}

			return Mapper.Map<IEnumerable<Page>>(source);
		}

		public IEnumerable<string> AllTags()
		{
			return new List<string>(Pages.Select(p => p.Tags));
		}

		public Page GetPageByTitle(string title)
		{
			var source = Pages.FirstOrDefault(p => p.Title == title);
			return Mapper.Map<Page>(source);
		}

		public PageContent GetPageContentById(Guid id)
		{
			var source = PageContents.FirstOrDefault(p => p.Id == id);
			return Mapper.Map<PageContent>(source);
		}

		public PageContent GetPageContentByPageIdAndVersionNumber(int id, int versionNumber)
		{
			var source = PageContents.FirstOrDefault(p => p.Page.Id == id && p.VersionNumber == versionNumber);
			return Mapper.Map<PageContent>(source);
		}

		public PageContent GetPageContentByEditedBy(string username)
		{
			var source = PageContents.FirstOrDefault(p => p.EditedBy == username);
			return Mapper.Map<PageContent>(source);
		}

		public IEnumerable<PageContent> FindPageContentsByPageId(int pageId)
		{
			var source = PageContents.Where(p => p.Page.Id == pageId);
			return Mapper.Map<IEnumerable<PageContent>>(source);
		}

		public IEnumerable<PageContent> AllPageContents()
		{
			var source = PageContents.ToList();
			return Mapper.Map<IEnumerable<PageContent>>(source);
		}

		public User GetAdminById(Guid id)
		{
			var source = Users.FirstOrDefault(x => x.Id == id && x.IsAdmin);
			return Mapper.Map<User>(source);
		}

		public User GetUserByActivationKey(string key)
		{
			var source = Users.FirstOrDefault(x => x.ActivationKey == key && x.IsActivated == false);
			return Mapper.Map<User>(source);
		}

		public User GetEditorById(Guid id)
		{
			var source = Users.FirstOrDefault(x => x.Id == id && x.IsEditor);
			return Mapper.Map<User>(source);
		}

		public User GetUserByEmail(string email, bool isActivated = true)
		{
			var source = Users.FirstOrDefault(x => x.Email == email && x.IsActivated == isActivated);
			return Mapper.Map<User>(source);
		}

		public User GetUserById(Guid id, bool isActivated = true)
		{
			var source = Users.FirstOrDefault(x => x.Id == id && x.IsActivated == isActivated);
			return Mapper.Map<User>(source);
		}

		public User GetUserByPasswordResetKey(string key)
		{
			var source = Users.FirstOrDefault(x => x.PasswordResetKey == key);
			return Mapper.Map<User>(source);
		}

		public User GetUserByUsername(string username)
		{
			var source = Users.FirstOrDefault(x => x.Username == username);
			return Mapper.Map<User>(source);
		}

		public User GetUserByUsernameOrEmail(string username, string email)
		{
			var source = Users.FirstOrDefault(x => x.Username == username || x.Email == email);
			return Mapper.Map<User>(source);
		}

		public IEnumerable<User> FindAllEditors()
		{
			var source = Users.Where(x => x.IsEditor);
			return Mapper.Map<IEnumerable<User>>(source);
		}

		public IEnumerable<User> FindAllAdmins()
		{
			var source = Users.Where(x => x.IsAdmin);
			return Mapper.Map<IEnumerable<User>>(source);
		}

		public PageContent GetPageContentByVersionId(Guid versionId)
		{
			var source = PageContents.FirstOrDefault(p => p.Id == versionId);
			return Mapper.Map<PageContent>(source);
		}

		public IEnumerable<PageContent> FindPageContentsEditedBy(string username)
		{
			var source = PageContents.Where(p => p.EditedBy == username);
			return Mapper.Map<IEnumerable<PageContent>>(source);
		}

		public void Dispose()
		{
			UnitOfWork.SaveChanges();
			UnitOfWork.Dispose();
		}

		public void SaveOrUpdatePage(Page page)
		{
			PageEntity entity = UnitOfWork.FindById<PageEntity>(page.Id);
			if (entity == null)
			{
				entity = Mapper.Map<PageEntity>(page);
				UnitOfWork.Add(entity);
			}
			else
			{
				MapPageToEntity(page, entity);
			}

			UnitOfWork.SaveChanges();
		}

		public PageContent AddNewPage(Page page, string text, string editedBy, DateTime editedOn)
		{
			PageEntity pageEntity = Mapper.Map<PageEntity>(page);
			pageEntity.Id = 0;
			UnitOfWork.Add(pageEntity);

			PageContentEntity pageContentEntity = new PageContentEntity()
			{
				Id = Guid.NewGuid(),
				Page = pageEntity,
				Text = text,
				EditedBy = editedBy,
				EditedOn = editedOn,
				VersionNumber = 1,
			};

			UnitOfWork.Add(pageContentEntity);
			UnitOfWork.SaveChanges();

			PageContent pageContent = new PageContent();
			pageContent.Page = page;
			MapEntityToPageContent(pageContentEntity, pageContent);

			return pageContent;
		}

		public PageContent AddNewPageContentVersion(Page page, string text, string editedBy, DateTime editedOn, int version)
		{
			PageEntity pageEntity = UnitOfWork.FindById<PageEntity>(page.Id);
			if (pageEntity != null)
			{
				PageContentEntity pageContentEntity = new PageContentEntity()
				{
					Id = Guid.NewGuid(),
					Page = pageEntity,
					Text = text,
					EditedBy = editedBy,
					EditedOn = editedOn,
					VersionNumber = version,
				};

				UnitOfWork.Add(pageContentEntity);
				UnitOfWork.SaveChanges();

				PageContent pageContent= new PageContent();
				pageContent.Page = page;
				MapEntityToPageContent(pageContentEntity, pageContent);
				return pageContent;
			}

			Log.Error("Unable to update page content for page id {0} (not found)", page.Id);
			return null;
		}

		public void SaveOrUpdateUser(User user)
		{
			UserEntity entity = UnitOfWork.FindById<UserEntity>(user.Id);
			if (entity == null)
			{
				entity = Mapper.Map<UserEntity>(user);
				UnitOfWork.Add(entity);
			}
			else
			{
				MapUserToEntity(user, entity);
			}

			UnitOfWork.SaveChanges();
		}

		public void UpdatePageContent(PageContent content)
		{
			PageContentEntity entity = UnitOfWork.FindById<PageContentEntity>(content.Id);
			if (entity != null)
			{
				MapPageContentToEntity(content, entity);
				UnitOfWork.SaveChanges();
			}
		}

		private void MapUserToEntity(User user, UserEntity entity)
		{
			entity.ActivationKey = user.ActivationKey;
			entity.Email = user.Email;
			entity.Firstname = user.Firstname;
			entity.IsActivated = user.IsActivated;
			entity.IsAdmin = user.IsAdmin;
			entity.IsEditor = user.IsEditor;
			entity.Lastname = user.Lastname;
			entity.Password = user.Password;
			entity.PasswordResetKey = user.PasswordResetKey;
			entity.Salt = user.Salt;
			entity.Username = user.Username;
		}

		private void MapPageToEntity(Page page, PageEntity entity)
		{
			entity.CreatedBy = page.CreatedBy;
			entity.CreatedOn = page.CreatedOn;
			entity.ModifiedBy = page.ModifiedBy;
			entity.ModifiedBy = page.ModifiedBy;
			entity.Tags = page.Tags;
			entity.Title = page.Title;
		}

		private void MapPageContentToEntity(PageContent pageContent, PageContentEntity entity)
		{
			entity.EditedOn = pageContent.EditedOn;
			entity.EditedBy = pageContent.EditedBy;
			entity.Text = pageContent.Text;
			entity.VersionNumber = pageContent.VersionNumber;
		}

		private void MapEntityToPageContent(PageContentEntity entity, PageContent pageContent)
		{
			pageContent.Id = entity.Id;
			pageContent.EditedOn = entity.EditedOn;
			pageContent.EditedBy = entity.EditedBy;
			pageContent.Text = entity.Text;
			pageContent.VersionNumber = entity.VersionNumber;
		}
	}
}