using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ADX.CRM.CreateMarketMakerSecurities
{
    public class CreateMarketMakerSecurities : CodeActivity
    {
      
        [Input("Market Maker JSON")]
        [RequiredArgument]
        public InArgument<string> MarketMakerJSON { get; set; }
        [Output("Status Code")]
        public OutArgument<int> StatusCode { get; set; }

        [Output("Message")]
        public OutArgument<string> Message { get; set; }
        [Output("Justification")]
        public OutArgument<string> Justification { get; set; }


        protected override void Execute(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            var service = serviceFactory.CreateOrganizationService(workflowContext.UserId);
            ITracingService tracingService = context.GetExtension<ITracingService>();

            var marketMakerjson = MarketMakerJSON.Get(context);
            var marketMaker = JsonSerializer.Deserialize<MarketMaker>(marketMakerjson);
            var marketMakerCode = marketMaker.MarketMakerCode != null ? marketMaker.MarketMakerCode : "";

            // get the market maker code from the account entity 
            try
            {
                var query = new QueryExpression("account");
                query.ColumnSet.AddColumns("adx_marketmakercode", "name");
                query.Criteria.AddCondition("adx_accountprofiletype", ConditionOperator.Equal, 4); // market maker profile type
                query.Criteria.AddCondition("adx_marketmakercode", ConditionOperator.Equal, marketMakerCode);

                var accountColl = service.RetrieveMultiple(query);
                if (accountColl != null && accountColl.Entities.Any())
                {

                    var accountId = accountColl.Entities[0].Id;
                    var accountName = accountColl.Entities[0].Contains("name") ? accountColl.Entities[0]["name"] : "";
                    foreach (var etn in marketMaker.SecurityObligations)
                    {
                        var obligatedRate = etn.ObligatedRate != null ? etn.ObligatedRate : 0 ;
                        var actualRate = etn.ActualRate != null ? etn.ActualRate : 0;
                        Entity sec = new Entity("adx_securityobligation");
                        sec["adx_marketmakername"] = new EntityReference("account", accountId);
                        sec["adx_marketmakercode"] = marketMakerCode;
                        sec["adx_name"] = accountName;
                        sec["adx_obligatedresponserate"] = obligatedRate;
                        sec["adx_actualresponserate"] = actualRate;
                        service.Create(sec);

                    }
                    StatusCode.Set(context, 200);
                    Message.Set(context, "Sucess");
                    Justification.Set(context, $"There are {marketMaker.SecurityObligations.Count} Securities Created Under {marketMakerCode}");

                }
                else
                {
                    StatusCode.Set(context, 400);
                    Message.Set(context, "Not Found");
                    Justification.Set(context, "there is no Market Maker with Code = " + marketMakerCode + " exist");

                }

            }
            catch (Exception ex) 
            {

                StatusCode.Set(context, 400);
                Message.Set(context, "Something Went Wrong ");
                Justification.Set(context, ex.Message);
            }
            

        }
        public class SecurityObligation
        {
            public decimal? ObligatedRate { get; set; }
            public decimal? ActualRate { get; set; }
            public string Name { get; set; }
        }

        public class MarketMaker
        {
            public string MarketMakerCode { get; set; }    
            public List<SecurityObligation> SecurityObligations { get; set;}
        }
    }
}
