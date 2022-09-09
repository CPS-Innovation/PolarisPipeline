﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using coordinator.Domain;
using coordinator.Domain.DocumentExtraction;
using coordinator.Domain.Exceptions;
using coordinator.Domain.Tracker;
using coordinator.Functions;
using coordinator.Functions.ActivityFunctions;
using coordinator.Functions.SubOrchestrators;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace coordinator.tests.Functions
{
    public class CoordinatorOrchestratorTests
    {
        private Fixture _fixture;
        private CoordinatorOrchestrationPayload _payload;
        private string _accessToken;
        private CaseDocument[] _caseDocuments;
        private string _transactionId;
        private List<TrackerDocument> _trackerDocuments;

        private Mock<IConfiguration> _mockConfiguration;
        private Mock<ILogger<CoordinatorOrchestrator>> _mockLogger;
        private Mock<IDurableOrchestrationContext> _mockDurableOrchestrationContext;
        private Mock<ITracker> _mockTracker;

        private CoordinatorOrchestrator CoordinatorOrchestrator;

        public CoordinatorOrchestratorTests()
        {
            _fixture = new Fixture();
            _accessToken = _fixture.Create<string>();
            _payload = _fixture.Build<CoordinatorOrchestrationPayload>()
                        .With(p => p.ForceRefresh, false)
                        .With(p => p.AccessToken, _accessToken)
                        .Create();
            _caseDocuments = _fixture.Create<CaseDocument[]>();
            _transactionId = _fixture.Create<string>();
            _trackerDocuments = _fixture.Create<List<TrackerDocument>>();

            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<CoordinatorOrchestrator>>();
            _mockDurableOrchestrationContext = new Mock<IDurableOrchestrationContext>();
            _mockTracker = new Mock<ITracker>();

            _mockConfiguration.Setup(config => config["CoordinatorOrchestratorTimeoutSecs"]).Returns("300");

            _mockDurableOrchestrationContext.Setup(context => context.GetInput<CoordinatorOrchestrationPayload>())
                .Returns(_payload);
            _mockDurableOrchestrationContext.Setup(context => context.InstanceId)
                .Returns(_transactionId);
            _mockDurableOrchestrationContext.Setup(context => context.CreateEntityProxy<ITracker>(It.Is<EntityId>(e => e.EntityName == nameof(Tracker).ToLower() && e.EntityKey == _payload.CaseId.ToString())))
                .Returns(_mockTracker.Object);
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<string>(nameof(GetOnBehalfOfAccessToken), _payload.AccessToken))
                .ReturnsAsync(_accessToken);
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<CaseDocument[]>(nameof(GetCaseDocuments), It.Is<GetCaseDocumentsActivityPayload>(p => p.CaseId == _payload.CaseId && p.AccessToken == _payload.AccessToken)))
                .ReturnsAsync(_caseDocuments);

            _mockTracker.Setup(tracker => tracker.GetDocuments()).ReturnsAsync(_trackerDocuments);

            CoordinatorOrchestrator = new CoordinatorOrchestrator(_mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Run_ThrowsWhenPayloadIsNull()
        {
            _mockDurableOrchestrationContext.Setup(context => context.GetInput<CoordinatorOrchestrationPayload>())
                .Returns(default(CoordinatorOrchestrationPayload));

            await Assert.ThrowsAsync<ArgumentException>(() => CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object));
        }

        [Fact]
        public async Task Run_ReturnsDocumentsWhenTrackerAlreadyProcessedAndForceRefreshIsFalse()
        {
            _mockTracker.Setup(tracker => tracker.IsAlreadyProcessed()).ReturnsAsync(true);

            var documents = await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            documents.Should().BeEquivalentTo(_trackerDocuments);
        }

        [Fact]
        public async Task Run_DoesNotInitialiseWhenTrackerAlreadyProcessedAndForceRefreshIsFalse()
        {
            _mockTracker.Setup(tracker => tracker.IsAlreadyProcessed()).ReturnsAsync(true);

            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            _mockTracker.Verify(tracker => tracker.Initialise(_transactionId), Times.Never);
        }

        [Fact]
        public async Task Run_Tracker_InitialisesWheTrackerIsAlreadyProcessedAndForceRefreshIsTrue()
        {
            _mockTracker.Setup(tracker => tracker.IsAlreadyProcessed()).ReturnsAsync(true);
            _payload.ForceRefresh = true;

            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            _mockTracker.Verify(tracker => tracker.Initialise(_transactionId));
        }

        [Fact]
        public async Task Run_Tracker_Initialises()
        {
            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            _mockTracker.Verify(tracker => tracker.Initialise(_transactionId));
        }

        [Fact]
        public async Task Run_Tracker_RegistersDocumentsNotFoundInCDEWhenCaseDocumentsIsEmpty()
        {
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<CaseDocument[]>(nameof(GetCaseDocuments), It.Is<GetCaseDocumentsActivityPayload>(p => p.CaseId == _payload.CaseId && p.AccessToken == _accessToken)))
                .ReturnsAsync(new CaseDocument[] { });

            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            _mockTracker.Verify(tracker => tracker.RegisterNoDocumentsFoundInCDE());
        }


        [Fact]
        public async Task Run_ReturnsEmptyListOfDocumentsWhenCaseDocumentsIsEmpty()
        {
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<CaseDocument[]>(nameof(GetCaseDocuments), It.Is<GetCaseDocumentsActivityPayload>(p => p.CaseId == _payload.CaseId && p.AccessToken == _accessToken)))
                .ReturnsAsync(new CaseDocument[] { });

            var documents = await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            documents.Should().BeEmpty();
        }


        [Fact]
        public async Task Run_Tracker_RegistersDocumentIds()
        {
            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            var documentIds = _caseDocuments.Select(d => d.DocumentId);
            _mockTracker.Verify(tracker => tracker.RegisterDocumentIds(documentIds));
        }

        [Fact]
        public async Task Run_CallsSubOrchestratorForEachDocumentId()
        {
            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            foreach (var document in _caseDocuments)
            {
                _mockDurableOrchestrationContext.Verify(
                    context => context.CallSubOrchestratorAsync(
                        nameof(CaseDocumentOrchestrator),
                        It.Is<CaseDocumentOrchestrationPayload>(p => p.CaseId == _payload.CaseId && p.DocumentId == document.DocumentId && p.AccessToken == _accessToken)));
            }
        }

        [Fact]
        public async Task Run_DoesNotThrowWhenSubOrchestratorCallFails()
        {
            _mockDurableOrchestrationContext.Setup(
                context => context.CallSubOrchestratorAsync(nameof(CaseDocumentOrchestrator), It.IsAny<CaseDocumentOrchestrationPayload>()))
                    .ThrowsAsync(new Exception());
            try
            {
                await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);
            }
            catch (Exception)
            {
                Assert.True(false);
            }
        }

        [Fact]
        public async Task Run_ThrowsCoordinatorOrchestrationExceptionWhenAllDocumentsHaveFailed()
        {
            _mockTracker.Setup(t => t.AllDocumentsFailed()).ReturnsAsync(true);

            await Assert.ThrowsAsync<CoordinatorOrchestrationException>(() => CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object));
        }

        [Fact]
        public async Task Run_Tracker_RegistersCompleted()
        {
            await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            _mockTracker.Verify(tracker => tracker.RegisterCompleted());
        }

        [Fact]
        public async Task Run_ReturnsDocuments()
        {
            var documents = await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);

            documents.Should().BeEquivalentTo(_trackerDocuments);
        }

        [Fact]
        public async Task Run_ThrowsExceptionWhenExceptionOccurs()
        {
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<CaseDocument[]>(nameof(GetCaseDocuments), It.Is<GetCaseDocumentsActivityPayload>(p => p.CaseId == _payload.CaseId && p.AccessToken == _accessToken)))
                .ThrowsAsync(new Exception("Test Exception"));

            await Assert.ThrowsAsync<Exception>(() => CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object));
        }

        [Fact]
        public async Task Run_Tracker_RegistersFailedWhenExceptionOccurs()
        {
            _mockDurableOrchestrationContext.Setup(context => context.CallActivityAsync<CaseDocument[]>(nameof(GetCaseDocuments), It.Is<GetCaseDocumentsActivityPayload>(p => p.CaseId == _payload.CaseId && p.AccessToken == _accessToken)))
                .ThrowsAsync(new Exception("Test Exception"));

            try
            {
                await CoordinatorOrchestrator.Run(_mockDurableOrchestrationContext.Object);
                Assert.False(true);
            }
            catch
            {
                _mockTracker.Verify(tracker => tracker.RegisterFailed());
            }
        }
    }
}
