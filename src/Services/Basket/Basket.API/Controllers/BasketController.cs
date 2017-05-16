﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopOnContainers.Services.Basket.API.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Abstractions;
using Basket.API.IntegrationEvents.Events;
using Microsoft.eShopOnContainers.Services.Basket.API.Services;
using Basket.API.Model;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Controllers
{
    //TODO NOTE: Right now this is a very chunky API, as the app evolves it is possible we would
    //want to make the actions more fine grained, add basket item as an action for example.
    //If this is the case we should also investigate changing the serialization format used for Redis,
    //using a HashSet instead of a simple string.
    [Route("/")]
    [Authorize]
    public class BasketController : Controller
    {
        private readonly IBasketRepository _repository;
        private readonly IIdentityService _identitySvc;
        private readonly IEventBus _eventBus;

        public BasketController(IBasketRepository repository, 
            IIdentityService identityService,
            IEventBus eventBus)
        {
            _repository = repository;
            _identitySvc = identityService;
            _eventBus = eventBus;
        }
        // GET api/values/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var basket = await _repository.GetBasketAsync(id);

            return Ok(basket);
        }

        // POST api/values
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]CustomerBasket value)
        {
            var basket = await _repository.UpdateBasketAsync(value);

            return Ok(basket);
        }

        [Route("checkout")]
        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody]BasketCheckout value, [FromHeader(Name = "x-requestid")] string requestId)
        {
            var userId = _identitySvc.GetUserIdentity();
            value.RequestId = (Guid.TryParse(requestId, out Guid guid) && guid != Guid.Empty) ?
                guid : value.RequestId;

            var basket = await _repository.GetBasketAsync(userId);
            var eventMessage = new UserCheckoutAcceptedIntegrationEvent(userId, value.City, value.Street,
                value.State, value.Country, value.ZipCode, value.CardNumber, value.CardHolderName,
                value.CardExpiration, value.CardSecurityNumber, value.CardTypeId, value.Buyer, value.RequestId, basket);

            // Once basket is checkout, sends an integration event to
            // ordering.api to convert basket to order and proceeds with
            // order creation process
            _eventBus.Publish(eventMessage);

            if (basket == null)
            {
                return BadRequest();
            }

            return Accepted();
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
            _repository.DeleteBasketAsync(id);
        }

    }
}
