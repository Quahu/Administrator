﻿using System;
using System.Threading.Tasks;
using Administrator.Services;
using Discord;

namespace Administrator.Common
{
    public abstract class Paginator : IDisposable
    {
        private readonly PaginationService _service;

        protected Paginator(IUserMessage message, IEmote[] emotes, PaginationService service = null)
        {
            Message = message;
            _service = service;
            Emotes = emotes;
            _service?.AddPaginator(this);
        }

        public IUserMessage Message { get; }

        public IEmote[] Emotes { get; }

        public abstract ValueTask<Page> GetPageAsync(IUser user, IEmote emote);

        public abstract Task CloseAsync();

        public virtual void Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
            _service?.RemovePaginator(this);
        }

        public override bool Equals(object obj)
        {
            return obj is Paginator other && other.Message.Id == Message.Id;
        }

        public override int GetHashCode()
        {
            return Message != null ? Message.GetHashCode() : 0;
        }
    }
}