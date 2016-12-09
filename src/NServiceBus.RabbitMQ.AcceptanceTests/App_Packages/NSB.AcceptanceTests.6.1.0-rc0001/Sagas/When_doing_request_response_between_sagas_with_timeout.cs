namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_doing_request_response_between_sagas_with_timeout : When_doing_request_response_between_sagas
    {
        [Test]
        public async Task Should_autocorrelate_the_response_back_to_the_requesting_saga_from_timeouts()
        {
            var context = await Scenario.Define<Context>(c => { c.ReplyFromTimeout = true; })
                .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new InitiateRequestingSaga())))
                .Done(c => c.DidRequestingSagaGetTheResponse)
                .Run(TimeSpan.FromSeconds(15));

            Assert.True(context.DidRequestingSagaGetTheResponse);
        }
    }
}