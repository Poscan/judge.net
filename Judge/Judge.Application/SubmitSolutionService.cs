﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Principal;
using System.Web;
using Judge.Application.Interfaces;
using Judge.Application.ViewModels.Problems.Solution;
using Judge.Application.ViewModels.Submit;
using Judge.Data;
using Judge.Model.CheckSolution;
using Judge.Model.Configuration;
using Judge.Model.SubmitSolution;

namespace Judge.Application
{
    internal sealed class SubmitSolutionService : ISubmitSolutionService
    {
        private readonly IUnitOfWorkFactory _factory;
        private readonly IPrincipal _principal;

        public SubmitSolutionService(IUnitOfWorkFactory factory, IPrincipal principal)
        {
            _factory = factory;
            _principal = principal;
        }

        public IEnumerable<LanguageViewModel> GetLanguages()
        {
            using (var uow = _factory.GetUnitOfWork(false))
            {
                var languageRepository = uow.GetRepository<ILanguageRepository>();

                return languageRepository.GetLanguages().Select(o => new LanguageViewModel
                {
                    Id = o.Id,
                    Name = o.Name
                }).AsEnumerable();
            }
        }

        public void SubmitSolution(long problemId, int selectedLanguage, HttpPostedFileBase file, long userId, string userHost)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            string sourceCode;
            using (var sr = new StreamReader(file.InputStream))
            {
                sourceCode = sr.ReadToEnd();
            }

            var submit = ProblemSubmit.Create();

            submit.ProblemId = problemId;
            submit.LanguageId = selectedLanguage;
            submit.UserId = userId;
            submit.FileName = file.FileName;
            submit.SourceCode = sourceCode;
            submit.UserHost = userHost;

            using (var unitOfWork = _factory.GetUnitOfWork(true))
            {
                var submitRepository = unitOfWork.GetRepository<ISubmitRepository>();
                submitRepository.Add(submit);
                unitOfWork.Commit();
            }
        }

        public SolutionViewModel GetSolution(long submitId, long userId)
        {
            using (var unitOfWork = _factory.GetUnitOfWork(false))
            {
                var submitRepository = unitOfWork.GetRepository<ISubmitRepository>();
                var taskRepository = unitOfWork.GetRepository<ITaskRepository>();

                var submit = submitRepository.Get(submitId);
                if (submit == null)
                    return null;

                var hasPermission = _principal.IsInRole("admin");

                if (!hasPermission && submit.UserId != userId)
                {
                    throw new AuthenticationException();
                }

                var problem = taskRepository.Get(submit.ProblemId);

                if (!problem.IsOpened)
                {
                    throw new InvalidOperationException();
                }

                return new SolutionViewModel
                {
                    ProblemId = submit.ProblemId,
                    SourceCode = submit.SourceCode,
                    ProblemName = problem.Name
                };
            }
        }
    }
}
